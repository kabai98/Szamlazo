namespace Szamlazo.Domain.Enums;

public enum SpendingCategory
{
    Groceries,
    Dining,
    Household,
    Transport,
    Housing,
    Health,
    Entertainment,
    Other
}

public static class SpendingCategories
{
    private static readonly IReadOnlyDictionary<SpendingCategory, string> ByCategory =
        new Dictionary<SpendingCategory, string>
        {
            [SpendingCategory.Groceries] = "Élelmiszer",
            [SpendingCategory.Dining] = "Étkezés",
            [SpendingCategory.Household] = "Háztartás",
            [SpendingCategory.Transport] = "Közlekedés",
            [SpendingCategory.Housing] = "Lakhatás",
            [SpendingCategory.Health] = "Egészség",
            [SpendingCategory.Entertainment] = "Szórakozás",
            [SpendingCategory.Other] = "Egyéb"
        };

    private static readonly IReadOnlyDictionary<string, SpendingCategory> ByLabel =
        ByCategory.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static IEnumerable<string> Labels => ByCategory.Values;
    public static string Label(SpendingCategory category) => ByCategory[category];
    public static SpendingCategory? FromLabel(string? label) =>
        label is not null && ByLabel.TryGetValue(label, out var category) ? category : null;
    public static SpendingCategory Parse(string label) => ByLabel[label];
}
