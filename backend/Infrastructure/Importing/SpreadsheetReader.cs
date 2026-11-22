using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

using static Szamlazo.Infrastructure.Importing.DocumentValues;

internal static class SpreadsheetReader
{
    public static ImportPreview Read(Stream stream, string extension, CancellationToken token)
    {
        try
        {
            if (extension == ".xlsx")
            {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                if (zip.Entries.Count > 500 || zip.Entries.Sum(entry => entry.Length) > 32 * 1024 * 1024)
                    throw new InvalidDataException("A munkafüzet túl nagy. Oszd kisebb fájlokra.");
                stream.Position = 0;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var sheet = extension == ".csv"
                ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(1250) })
                : ExcelReaderFactory.CreateReader(stream);
            if (!sheet.Read())
                throw new InvalidDataException("Üres táblázat.");

            var columns = Enumerable.Range(0, sheet.FieldCount).Select(i => Normalize(sheet.GetValue(i)?.ToString() ?? "")).ToArray();
            var name = FindColumn(columns, ["NEV", "KERESKEDO", "BOLT", "PARTNER", "MEGNEVEZES", "MERCHANT", "DESCRIPTION"]);
            var date = FindColumn(columns, ["DATUM", "VASARLAS DATUMA", "DATE"]);
            var amount = FindColumn(columns, ["OSSZEG", "OSSZEG (FT)", "VEGOSSZEG", "AMOUNT", "TOTAL"]);
            var category = FindColumn(columns, ["KATEGORIA", "CATEGORY"]);
            if (name < 0 || date < 0 || amount < 0)
                throw new InvalidDataException("Az első munkalap fejlécében legyen Név, Dátum és Összeg oszlop. A Kategória oszlop opcionális.");

            var rows = new List<ImportRow>();
            var warnings = new List<string>();
            for (var index = 2; sheet.Read(); index++)
            {
                token.ThrowIfCancellationRequested();
                if (index > 5000 || rows.Count == 200)
                {
                    warnings.Add("A fájl elejét olvastuk be. A többi sort külön fájlban importálhatod.");
                    break;
                }

                var merchant = sheet.GetValue(name)?.ToString()?.Trim();
                var dateValue = sheet.GetValue(date);
                var amountValue = sheet.GetValue(amount);
                if (string.IsNullOrWhiteSpace(merchant) && dateValue is null && amountValue is null)
                    continue;

                var parsedDate = dateValue is DateTime value ? DateOnly.FromDateTime(value) : ParseDate(dateValue?.ToString());
                var parsedAmount = amountValue is double or int or decimal
                    ? ParseAmount(Convert.ToString(amountValue, CultureInfo.InvariantCulture))
                    : ParseAmount(amountValue?.ToString());
                var label = category >= 0 ? Normalize(sheet.GetValue(category)?.ToString() ?? "") : "";
                var parsedCategory = SpendingCategories.Labels.FirstOrDefault(x => Normalize(x) == label) ?? ReceiptParser.SuggestCategory(merchant ?? "");
                if (string.IsNullOrWhiteSpace(merchant) || parsedDate is null ||
                    parsedAmount is null || !Money.IsValid(parsedAmount.Value, positive: true))
                    warnings.Add($"A(z) {index}. sorban hiányos vagy hibás adat van.");

                rows.Add(new ImportRow(merchant, parsedDate, parsedAmount, parsedCategory, ExpenseSource.Spreadsheet, []));
            }

            if (rows.Count == 0)
                throw new InvalidDataException("A táblázatban nincsenek importálható sorok.");
            return new ImportPreview(rows, warnings);
        }
        catch (Exception ex) when (ex is ExcelDataReader.Exceptions.ExcelReaderException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("A táblázat nem olvasható. Ellenőrizd a formátumot és a kódolást.", ex);
        }
    }

    private static int FindColumn(string[] columns, string[] names) => Array.FindIndex(columns, names.Contains);

}
