using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Szamlazo.Domain.Enums;
using Szamlazo.Infrastructure.Importing;
using Xunit;

namespace Szamlazo.Tests;

public sealed class ReceiptImportTests
{
    [Theory]
    [InlineData("B00 Tej 399", "Tej")]
    [InlineData("BOO Tej 399", "Tej")]
    [InlineData("C00 Kenyér 399", "Kenyér")]
    [InlineData("COO Kenyér 399", "Kenyér")]
    [InlineData("Tej 399 B00", "Tej")]
    [InlineData("Tej 399 BOO", "Tej")]
    [InlineData("Kenyér 399 C00", "Kenyér")]
    [InlineData("Kenyér 399 COO", "Kenyér")]
    public void Vat_markers_locate_items_with_or_without_receipt_heading(string line, string name)
    {
        var row = ReceiptParser.Parse($"SPAR\n{line}\nÖSSZESEN: 399 Ft", ExpenseSource.Image).Rows.Single();
        var item = Assert.Single(row.Items);
        Assert.Equal(name, item.Name);
        Assert.Equal(399m, item.Amount);
        Assert.Equal(ExpenseItemKind.Product, item.Kind);
    }

    [Theory]
    [InlineData("eC00 Akció -100")]
    [InlineData("Akció -100 eCOO")]
    public void Vat_marker_on_discount_does_not_make_it_a_product(string line)
    {
        var row = ReceiptParser.Parse($"SPAR\nNYUGTA\nTEJ 399 B00\n{line}\nÖSSZESEN: 299 Ft", ExpenseSource.Image).Rows.Single();
        Assert.Equal(ExpenseItemKind.Discount, row.Items.Single(item => item.Amount < 0).Kind);
        Assert.Equal(299m, row.Items.Sum(item => item.Amount));
    }

    [Fact]
    public void Candidate_selection_prefers_receipt_evidence_over_background_noise()
    {
        Assert.True(ImageOcrService.Score("SPAR\nNYUGTA\nÖSSZESEN: 1725 Ft") >
            ImageOcrService.Score("LTA\nWA\nAUTH BTXGS4\n2912"));
    }

    [Fact]
    public void Subtotal_and_savings_are_not_purchases_or_additional_discounts()
    {
        var preview = ReceiptParser.Parse("""
            TESCO-GLOBAL ZRT.
            NYUGTA
            TS PENNE 500G      325 C00
            APENTA BODY       449 C00
            PET VISSZAV. DIJ    50 E00
            MAGYAR TEJF       399 B00
            MAGGI KOCKA       419 C00
            BANAN             354 C00
            BURGONYA          349 C00
            RÉSZÖSSZEG:     2 345 Ft
            CC APENTA BODY   -180 eC00
            ÖSSZESEN:       2 165 Ft
            BANKKÁRTYA:     2 165 Ft
            Megtakarítás:     180 Ft
            2026.11.18 19:25
            """, ExpenseSource.Image);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(2165m, row.Amount);
        Assert.Equal(8, row.Items.Count);
        Assert.Equal(row.Amount, row.Items.Sum(item => item.Amount));
        Assert.Equal(ExpenseItemKind.Discount, row.Items.Single(item => item.Amount < 0).Kind);
        Assert.Equal(new DateOnly(2026, 11, 18), row.Date);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public void Weighted_quantity_is_separate_from_package_size_and_duplicate_products_remain()
    {
        var row = ReceiptParser.Parse("""
            SPAR
            NYUGTA
            B00 TEJ 500G 499
            B00 TEJ 500G 499
            C00 CITROM KG
            0,194 KG x 899 Ft/KG 174
            ÖSSZESEN: 1172 Ft
            """, ExpenseSource.Image).Rows.Single();

        Assert.Equal(3, row.Items.Count);
        Assert.Null(row.Items[0].Quantity);
        Assert.Equal(0.194m, row.Items[2].Quantity);
        Assert.Equal(899m, row.Items[2].UnitPrice);
        Assert.Equal("kg", row.Items[2].Unit);
    }

    [Fact]
    public void Incomplete_receipt_and_promotional_date_require_review()
    {
        var preview = ReceiptParser.Parse("""
            TESCO
            NYUGTA
            TEJ 399 B00
            ÖSSZESEN: 2165 Ft
            PONTGYŰJTÉS 2027.01.17-IG TART
            """, ExpenseSource.Image);
        Assert.Null(preview.Rows.Single().Date);
        Assert.Contains(preview.Warnings, warning => warning.Contains("eltér"));
    }

    [Fact]
    public async Task Spreadsheet_route_does_not_start_an_ocr_process()
    {
        var reader = CreateReader();
        using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "bolt;datum;osszeg\nLidl;2026-11-18;5250\n"));
        var preview = await reader.ReadAsync("expenses.csv", content, default);
        Assert.NotEmpty(preview.Rows);
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected_before_reading()
    {
        using var content = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateReader().ReadAsync("receipt.exe", content, default));
    }

    private static DocumentReader CreateReader()
    {
        var configuration = new ConfigurationBuilder().Build();
        var tools = new ExternalToolRunner(configuration, NullLogger<ExternalToolRunner>.Instance);
        var images = new ImageOcrService(configuration, tools);
        return new DocumentReader(configuration, new PdfTextReader(tools, images), images);
    }
}
