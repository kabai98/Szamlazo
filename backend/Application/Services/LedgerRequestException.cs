namespace Szamlazo.Application.Services;

public sealed class LedgerRequestException(string message, bool missing = false) : Exception(message)
{
    public bool IsMissing { get; } = missing;
    public static LedgerRequestException NotFound() => new("A kiadás nem található.", missing: true);
}
