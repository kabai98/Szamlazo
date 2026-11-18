namespace Szamlazo.Domain;

public static class Money
{
    public static bool IsValid(decimal value, bool positive) =>
        value is > -1_000_000_000m and < 1_000_000_000m && (!positive || value > 0) && decimal.Round(value, 2) == value;

    public static decimal FromMinor(long amount) => amount / 100m;
    public static long ToMinor(decimal amount) => (long)(amount * 100m);
}
