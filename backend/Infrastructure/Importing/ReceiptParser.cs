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

        return new ImportPreview([new(merchant, date, total, SuggestCategory(merchant ?? ""), source, items, normalized.Any(line => line.Contains("MASTERCARD")) ? "Mastercard" : null, ReadTime(text))],
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

    private static TimeOnly? ReadTime(string text)
    {
        var match = Regex.Match(text, @"20\d{2}[.\/-]\s*\d{1,2}[.\/-]\s*\d{1,2}\.?\s+(\d{2}:\d{2})");
        return match.Success && TimeOnly.TryParse(match.Groups[1].Value, out var time) ? time : null;
    }

    private static List<ItemInput> ReadItems(string[] lines, string[] normalized, int end, string? merchant)
    {
        var items = new List<ItemInput>();
        string? pending = null;
        var header = Array.FindIndex(normalized, line => Regex.IsMatch(line, @"(MEGNEVEZES|TERMEK).*(OSSZEG|EGYSEGAR|MENNYISEG|BRUTTO)"));
        if (end == lines.Length)
            return items;
        var first = header >= 0 && header < end ? header + 1 : 1;
        for (var i = first; i < end && items.Count < 100; i++)
        {
            var line = normalized[i];
            var tagged = Regex.Match(lines[i], @"^[ABC]\d{2}\s+(?<name>.+)", RegexOptions.IgnoreCase);
            if (tagged.Success)
                pending = tagged.Groups["name"].Value;
            var weighted = Regex.Match(line, @"(?<q>\d+[.,]\d+)\s*(?<u>KG|DB|L)\s*[X×*]\s*(?<p>\d+[.,]?\d*)\s*FT");
            if (weighted.Success && pending is not null)
            {
                var trailing = TrailingAmount().Match(lines[i]);
                decimal? amount = trailing.Success && trailing.Index > weighted.Index + weighted.Length
                    ? ParseAmount(trailing.Groups["amount"].Value) : null;
                if (amount is null && i + 1 < end && Regex.IsMatch(lines[i + 1], @"^\d[\d .,]*$"))
                    amount = ParseAmount(lines[++i]);
                if (amount.HasValue)
                {
                    items.Add(new ItemInput(pending, amount,
                        decimal.Parse(weighted.Groups["q"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                        weighted.Groups["u"].Value.ToLowerInvariant(), ParseAmount(weighted.Groups["p"].Value)));
                    pending = null;
                }
                continue;
            }

            if (ParseDate(lines[i]).HasValue || Regex.IsMatch(line,
                @"\b(ADOSZAM|NYUGTA|SZAMLA|PENZTAR|KASSZA|UGYFELSZOLGALAT|FIZETENDO|NETTO|AFA|OSSZESEN|TELEFON|UTCA|BANKKARTYA|KESZPENZ)\b"))
                continue;
            var price = TrailingAmount().Match(lines[i]);
            if (!price.Success || price.Index == 0)
                continue;
            var name = Regex.Replace(lines[i][..price.Index].Trim(' ', ':', '.'), @"^[ABC]\d{2}\s+", "");
            if (name.Length is < 2 or > 160 || name == merchant || !name.Any(char.IsLetter))
                continue;
            if (ParseAmount(price.Groups["amount"].Value) is { } value && Money.IsValid(value, positive: false))
                items.Add(new ItemInput(name, value));
        }
        return items;
    }

    public static string SuggestCategory(string value) =>
        Shops.Any(shop => Regex.IsMatch(Normalize(value), @"\b" + shop.Match + @"\b")) ? "Élelmiszer" : "Egyéb";

    [GeneratedRegex(@"(?<!\S)(?<amount>[-−]?(?:\d{1,3}(?:[ .,\u00a0\u202f]\d{3})+|\d+)(?:[.,]\d{1,2})?)(?:\s*(?:Ft\.?|HUF))?(?:\s+[A-E]\d{0,2})?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrailingAmount();
}
