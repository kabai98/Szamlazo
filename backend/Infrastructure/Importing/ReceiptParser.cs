using System.Globalization;
using System.Text.RegularExpressions;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

using static Szamlazo.Infrastructure.Importing.DocumentValues;

internal static partial class ReceiptParser
{
    private static readonly (string Match, string Name)[] Shops =
        [("LIDL", "Lidl"), ("SPAR", "SPAR"), ("ALDI", "ALDI"), ("PENNY", "PENNY"), ("TESCO", "Tesco"), ("AUCHAN", "Auchan")];

    public static ImportPreview Parse(string text, ExpenseSource source)
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
        date ??= lines.Where((_, index) => !Regex.IsMatch(normalized[index],
                @"\b(PONTGYUJTES|PROMOCIO|ERVENYES|TART|AKCIO)\b"))
            .Select(ParseDate).FirstOrDefault(value => value.HasValue);

        decimal? total = null;
        var totalLine = lines.Length;
        foreach (var pattern in new[] { @"FIZETENDO|FIZETNI|VEGOSSZEG", @"(?:^|\s)(?:MIND)?OSSZESEN\b", @"TOTAL|BRUTTO OSSZEG", @"MASTERCARD|VISA|BANKKARTYA|OSSZEG/AMOUNT" })
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
        if (items.Count == 0)
            warnings.Add("Nem sikerült terméktételeket felismerni. Add meg vagy ellenőrizd őket mentés előtt.");
        if (items.Count > 0 && total.HasValue && items.Sum(x => x.Amount) != total)
            warnings.Add("A felismert tételek összege eltér a végösszegtől. Ellenőrizd a tételeket és a kedvezményeket.");

        return new ImportPreview([new(merchant, date, total, SuggestCategory(merchant ?? ""), source, items, ReadPayment(normalized), ReadTime(text))],
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

    private static string? ReadPayment(string[] lines)
    {
        if (lines.Any(line => line.Contains("MASTERCARD"))) return "Mastercard";
        if (lines.Any(line => line.Contains("VISA"))) return "Visa";
        if (lines.Any(line => line.Contains("BANKKARTYA"))) return "Bankkártya";
        if (lines.Any(line => line.Contains("KESZPENZ"))) return "Készpénz";
        return null;
    }

    private static TimeOnly? ReadTime(string text)
    {
        var match = Regex.Match(text, @"20\d{2}[.\/-]\s*\d{1,2}[.\/-]\s*\d{1,2}\.?\s+(\d{2}:\d{2})");
        return match.Success && TimeOnly.TryParse(match.Groups[1].Value, out var time) ? time : null;
    }

    private static List<ItemInput> ReadItems(string[] lines, string[] normalized, int end, string? merchant)
    {
        var items = new List<ItemInput>();
        string? pendingName = null;
        (decimal Quantity, string Unit, decimal? Price)? pendingQuantity = null;
        var header = Array.FindIndex(normalized, line => line.Contains("MEGNEVEZES"));
        var receipt = Array.FindIndex(normalized, line => line.Contains("NYUGTA"));
        var first = Math.Max(header, receipt) + 1;
        if (first == 0)
        {
            first = Array.FindIndex(normalized, line => Regex.IsMatch(line,
                @"^[A-E][0O]{2}\s|[A-E][0O]{2}$"));
            if (first < 0)
                return items;
        }

        for (var index = first; index < end && items.Count < 100; index++)
        {
            var line = lines[index];
            var normalizedLine = normalized[index];
            if (Regex.IsMatch(normalizedLine,
                @"\b(ADOSZAM|NYUGTA|SZAMLA|PENZTAR|KASSZA|FIZETENDO|NETTO|AFA|OSSZESEN|TELEFON|BANKKARTYA|KESZPENZ|MEGTAKARITAS|RESZOSSZEG)\b") ||
                ParseDate(line).HasValue)
                continue;

            var quantity = Regex.Match(normalizedLine,
                @"(?<q>\d+(?:[.,]\d+)?)\s*(?<u>KG|DB|L)\s*[X×*]\s*(?<p>\d+(?:[.,]\d+)?)\s*FT\s*/?\s*(?:KG|DB|L)?");
            var trailing = TrailingAmount().Match(line);
            if (quantity.Success)
            {
                pendingQuantity = (decimal.Parse(quantity.Groups["q"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                    quantity.Groups["u"].Value.ToLowerInvariant(), ParseAmount(quantity.Groups["p"].Value));
                decimal? value = trailing.Success && trailing.Index >= quantity.Index + quantity.Length
                    ? ParseAmount(trailing.Groups["amount"].Value) : null;
                if (value is null && pendingName is not null && index + 1 < end &&
                    Regex.IsMatch(lines[index + 1], @"^\d[\d .,]*$"))
                    value = ParseAmount(lines[++index]);
                if (value.HasValue && pendingName is not null)
                {
                    AddItem(pendingName, value.Value);
                    pendingName = null;
                }
                continue;
            }

            var name = trailing.Success ? line[..trailing.Index].Trim(' ', ':', '.') : line;
            name = Regex.Replace(name, @"^(?:e)?[A-E][0O]{2}\s+", "", RegexOptions.IgnoreCase).Trim();
            if (name.Length is < 2 or > 160 || name == merchant || !name.Any(char.IsLetter))
                continue;
            if (trailing.Success && ParseAmount(trailing.Groups["amount"].Value) is { } amount &&
                Money.IsValid(amount, positive: false))
            {
                AddItem(name, amount);
                pendingName = null;
            }
            else if (Regex.IsMatch(normalizedLine, @"^(?:E)?[A-E][0O]{2}\s"))
            {
                pendingName = name;
            }
        }
        return items;

        void AddItem(string name, decimal amount)
        {
            items.Add(new ItemInput(name, amount, pendingQuantity?.Quantity,
                pendingQuantity?.Unit, pendingQuantity?.Price,
                amount < 0 ? ExpenseItemKind.Discount : ExpenseItemKind.Product));
            pendingQuantity = null;
        }
    }

    public static string SuggestCategory(string value) =>
        Shops.Any(shop => Regex.IsMatch(Normalize(value), @"\b" + shop.Match + @"\b")) ? "Élelmiszer" : "Egyéb";

    [GeneratedRegex(@"(?<!\S)(?<amount>[-−]?(?:\d{1,3}(?:[ .,\u00a0\u202f]\d{3})+|\d+)(?:[.,]\d{1,2})?)(?:\s*(?:Ft\.?|HUF))?(?:\s+(?:e)?[A-E][0O]{0,2})?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrailingAmount();
}
