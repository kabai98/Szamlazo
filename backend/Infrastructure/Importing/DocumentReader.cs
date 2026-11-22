using Microsoft.Extensions.Configuration;
using Szamlazo.Application.Contracts;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

internal sealed class DocumentReader(
    IConfiguration configuration, PdfTextReader pdf, ImageOcrService images) : IDocumentReader
{
    private static readonly string[] ImageExtensions = [".jfif", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff"];
    public async Task<ImportPreview> ReadAsync(string filename, Stream stream, CancellationToken request)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension is not (".csv" or ".xls" or ".xlsx" or ".pdf") && !ImageExtensions.Contains(extension))
            throw new InvalidDataException("Támogatott fájlok: JPG, PNG, WEBP, TIFF, PDF, XLS, XLSX és CSV.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request);
        deadline.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue("Ocr:TimeoutSeconds", 120)));
        if (extension is ".csv" or ".xls" or ".xlsx")
            return SpreadsheetReader.Read(stream, extension, deadline.Token);

        var temp = Directory.CreateTempSubdirectory("szamlazo-");
        try
        {
            var input = Path.Combine(temp.FullName, "upload" + extension);
            await using (var destination = File.Create(input))
                await stream.CopyToAsync(destination, deadline.Token);
            var text = extension == ".pdf"
                ? await pdf.ReadAsync(input, temp.FullName, deadline.Token)
                : await images.ReadAsync(input, deadline.Token);
            return ReceiptParser.Parse(text, extension == ".pdf" ? ExpenseSource.Pdf : ExpenseSource.Image);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

}
