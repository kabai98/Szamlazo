using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Szamlazo.Infrastructure.Importing;

internal static class DocumentValues
{
    private static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu-HU");
    public static DateOnly? ParseDate(string? value)
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

    public static decimal? ParseAmount(string? value)
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

    public static string Normalize(string value) => new string(value.Normalize(NormalizationForm.FormD)
        .Where(letter => CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark).ToArray())
        .ToUpperInvariant().Trim();

}
