using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using Szamlazo.Application.Contracts;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

internal sealed partial class DocumentReader(IConfiguration configuration) : IDocumentReader
{
    private static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu-HU");
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff"];
    private static readonly (string Match, string Name)[] Shops =
        [("LIDL", "Lidl"), ("SPAR", "SPAR"), ("ALDI", "ALDI"), ("PENNY", "PENNY"), ("TESCO", "Tesco"), ("AUCHAN", "Auchan")];

    public async Task<ImportPreview> ReadAsync(string filename, Stream stream, CancellationToken request)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension is not (".csv" or ".xls" or ".xlsx" or ".pdf") && !ImageExtensions.Contains(extension))
            throw new InvalidDataException("Támogatott fájlok: JPG, PNG, WEBP, TIFF, PDF, XLS, XLSX és CSV.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request);
        deadline.CancelAfter(TimeSpan.FromSeconds(75));
        if (extension is ".csv" or ".xls" or ".xlsx")
            return ReadSheet(stream, extension, deadline.Token);

        var temp = Directory.CreateTempSubdirectory("szamlazo-");
        try
        {
            var input = Path.Combine(temp.FullName, "upload" + extension);
            await using (var destination = File.Create(input))
                await stream.CopyToAsync(destination, deadline.Token);
            var text = extension == ".pdf"
                ? await ReadPdf(input, temp.FullName, deadline.Token)
                : await ReadImage(input, deadline.Token);
            return ReadReceipt(text, extension == ".pdf" ? ExpenseSource.Pdf : ExpenseSource.Image);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private async Task<string> ReadPdf(string path, string workspace, CancellationToken token)
    {
        var info = await RunAsync("pdfinfo", ["-enc", "UTF-8", path], token);
        var pages = Regex.Match(info, @"(?m)^Pages:\s*(\d+)");
        if (!pages.Success || !int.TryParse(pages.Groups[1].Value, out var count) || count is < 1 or > 5)
            throw new InvalidDataException("Egy legfeljebb 5 oldalas számla PDF-jét töltsd fel.");

        var output = new StringBuilder();
        for (var number = 1; number <= count; number++)
        {
            var page = number.ToString(CultureInfo.InvariantCulture);
            var text = await RunAsync("pdftotext", ["-f", page, "-l", page, "-layout", "-enc", "UTF-8", path, "-"], token);
            if (text.Count(char.IsLetterOrDigit) < 30)
            {
                var image = Path.Combine(workspace, "page");
                await RunAsync("pdftoppm", ["-f", page, "-l", page, "-scale-to", "1400", "-singlefile", "-png", path, image], token);
                text = await ReadImage(image + ".png", token);
            }
            output.AppendLine(text);
        }
        return output.ToString();
    }

    private Task<string> ReadImage(string path, CancellationToken token) =>
        RunAsync("tesseract", [path, "stdout", "-l", "hun+eng", "--psm", "6"], token);

    private async Task<string> RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = configuration[$"Tools:{executable}"] ?? executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
            start.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException();
        }
        catch (Win32Exception)
        {
            throw new InvalidDataException("A helyi szövegfelismerő eszköz hiányzik. A Docker-változat tartalmazza.");
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(token);
            var errors = process.StandardError.ReadToEndAsync(token);
            try
            {
                await process.WaitForExitAsync(token);
                await errors;
                if (process.ExitCode != 0)
                    throw new InvalidDataException("A fájl nem olvasható. Ellenőrizd, hogy megnyitható és nincs jelszóval védve.");
                return await output;
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
        }
    }

    private static ImportPreview ReadSheet(Stream stream, string extension, CancellationToken token)
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
                var parsedCategory = SpendingCategories.Labels.FirstOrDefault(x => Normalize(x) == label) ?? SuggestCategory(merchant ?? "");
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

    private static ImportPreview ReadReceipt(string text, ExpenseSource source)
    {
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace('\u00a0', ' ').Replace('\u202f', ' ')).ToArray();
        var normalized = lines.Select(Normalize).ToArray();
        var merchant = FindMerchant(lines, normalized);
        DateOnly? date = null;
        var dateLine = Array.FindIndex(normalized, line => line.Contains("SZAMLA KELTE") ||
            line.Contains("KIAL LITAS DATUMA") || line.Contains("KIALLITAS DATUMA") || line.StartsWith("DATUM"));
        if (dateLine >= 0)
            date = ParseDate(lines[dateLine]) ?? ParseDate(lines.ElementAtOrDefault(dateLine + 1));
        date ??= lines.Select(ParseDate).FirstOrDefault(value => value.HasValue);

        decimal? total = null;
        var totalLine = lines.Length;
        foreach (var pattern in new[] { @"FIZETENDO|FIZETNI|VEGOSSZEG", @"(?:^|\s)(?:MIND)?OSSZESEN\b", @"TOTAL|BRUTTO OSSZEG" })
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(normalized[i], pattern) || normalized[i].Contains("NETTO") ||
                    normalized[i].Contains("MEGTAKARITAS") || normalized[i].Contains("KEDVEZMENY OSSZESEN"))
                    continue;

                totalLine = Math.Min(totalLine, i);
                var match = TrailingAmount().Match(lines[i]);
                if (!match.Success && i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\d[\d., ]*(?:\s*(?:Ft|HUF))?$", RegexOptions.IgnoreCase))
                    match = TrailingAmount().Match(lines[i + 1]);
                total = match.Success ? ParseAmount(match.Groups["amount"].Value) : null;
                if (total is { } amount && Money.IsValid(amount, positive: true))
                    break;
                total = null;
            }
            if (total.HasValue)
                break;
        }

        var warnings = new List<string>();
        if (Regex.IsMatch(Normalize(text), @"\b(?:EUR|USD|GBP|CHF|RON|PLN|CZK|SEK|JPY)\b|[€$£]"))
        {
            total = null;
            warnings.Add("Más pénznem jelölését is megtaláltuk. Ellenőrizd és add meg a forintban fizetett összeget.");
        }
        if (merchant is null) warnings.Add("A kereskedő neve nem volt egyértelmű.");
        if (date is null) warnings.Add("A vásárlás dátuma nem volt egyértelmű.");
        if (total is null) warnings.Add("A fizetendő végösszeg ellenőrzést igényel.");

        var items = ReadItems(lines, normalized, totalLine, merchant);
        if (items.Count > 0 && total.HasValue && items.Sum(x => x.Amount) != total)
            warnings.Add("A felismert tételek összege eltér a végösszegtől. Ellenőrizd a tételeket és a kedvezményeket.");

        return new ImportPreview([new(merchant, date, total, SuggestCategory(merchant ?? ""), source, items)],
            warnings, text[..Math.Min(text.Length, 20000)].Trim());
    }

    private static string? FindMerchant(string[] lines, string[] normalized)
    {
        foreach (var line in normalized.Take(12))
        {
            foreach (var shop in Shops)
                if (Regex.IsMatch(line, @"\b" + shop.Match + @"\b"))
                    return shop.Name;
        }

        var seller = Array.FindIndex(normalized, line => Regex.IsMatch(line, @"^(ELADO|KIBOCSATO|SZALLITO)(\s|:)"));
        if (seller >= 0)
        {
            var afterLabel = lines[seller].Split(':', 2).ElementAtOrDefault(1)?.Trim();
            if (afterLabel is { Length: > 1 and <= 160 } && !Normalize(afterLabel).StartsWith("VEVO"))
                return afterLabel;
            if (seller + 1 < lines.Length)
                return Regex.Split(lines[seller + 1], @"\s{2,}").FirstOrDefault();
        }

        return lines.Take(6).FirstOrDefault(line => line.Length is > 2 and <= 160 &&
            char.IsLetter(line[0]) && !Regex.IsMatch(Normalize(line), @"^(SZAMLA|NYUGTA|ADOBIZONYLAT|DATUM|VEVO)\b"));
    }

    private static List<ItemInput> ReadItems(string[] lines, string[] normalized, int end, string? merchant)
    {
        var items = new List<ItemInput>();
        var header = Array.FindIndex(normalized, line => Regex.IsMatch(line, @"(MEGNEVEZES|TERMEK).*(OSSZEG|EGYSEGAR|MENNYISEG|BRUTTO)"));
        if (end == lines.Length)
            return items;
        var first = header >= 0 && header < end ? header + 1 : 1;
        for (var i = first; i < end && items.Count < 100; i++)
        {
            var line = normalized[i];
            if (ParseDate(lines[i]).HasValue || Regex.IsMatch(line,
                @"\b(ADOSZAM|NYUGTA|SZAMLA|PENZTAR|KASSZA|UGYFELSZOLGALAT|FIZETENDO|NETTO|AFA|OSSZESEN|TELEFON|UTCA|BANKKARTYA|KESZPENZ)\b"))
                continue;
            var price = TrailingAmount().Match(lines[i]);
            if (!price.Success || price.Index == 0)
                continue;
            var name = lines[i][..price.Index].Trim(' ', ':', '.');
            if (name.Length is < 2 or > 160 || name == merchant || !name.Any(char.IsLetter))
                continue;
            if (ParseAmount(price.Groups["amount"].Value) is { } value && Money.IsValid(value, positive: false))
                items.Add(new ItemInput(name, value));
        }
        return items;
    }

    private static int FindColumn(string[] columns, string[] names) => Array.FindIndex(columns, names.Contains);

    private static string SuggestCategory(string value) =>
        Shops.Any(shop => Regex.IsMatch(Normalize(value), @"\b" + shop.Match + @"\b")) ? "Élelmiszer" : "Egyéb";

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = Regex.Match(value, @"(?<!\d)(?<y>19\d\d|20\d\d)[.\/-]\s*(?<m>\d{1,2})[.\/-]\s*(?<d>\d{1,2})(?!\d)");
        if (match.Success)
        {
            var iso = $"{match.Groups["y"].Value}-{match.Groups["m"].Value.PadLeft(2, '0')}-{match.Groups["d"].Value.PadLeft(2, '0')}";
            return DateOnly.TryParseExact(iso, "yyyy-MM-dd", out var date) ? date : null;
        }
        return DateOnly.TryParseExact(value.Trim(), ["d/M/yyyy", "d.M.yyyy"], Hungarian, DateTimeStyles.None, out var other)
            ? other : null;
    }

    private static decimal? ParseAmount(string? value)
    {
        if (value is null) return null;
        value = Regex.Replace(value, @"(?i)(HUF|FT)|[\s\u00a0\u202f]", "").Replace('−', '-');
        if (value.Contains(',') && value.Contains('.'))
            value = value.LastIndexOf(',') > value.LastIndexOf('.') ? value.Replace(".", "") : value.Replace(",", "");
        if (Regex.IsMatch(value, @"^-?\d{1,3}(?:[.,]\d{3})+$"))
            value = value.Replace(".", "").Replace(",", "");
        value = value.Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string Normalize(string value) => new string(value.Normalize(NormalizationForm.FormD)
        .Where(letter => CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark).ToArray())
        .ToUpperInvariant().Trim();

    [GeneratedRegex(@"(?<!\S)(?<amount>[-−]?(?:\d{1,3}(?:[ .,\u00a0\u202f]\d{3})+|\d+)(?:[.,]\d{1,2})?)(?:\s*(?:Ft\.?|HUF))?(?:\s+[A-E]\d{0,2})?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrailingAmount();
}
