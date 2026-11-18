namespace Szamlazo.Domain.Entities;

public sealed class Budget
{
    public string OwnerId { get; set; } = "";
    public string Month { get; set; } = "";
    public long AmountMinor { get; set; }
}
