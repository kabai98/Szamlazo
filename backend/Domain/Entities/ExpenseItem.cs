namespace Szamlazo.Domain.Entities;

public sealed class ExpenseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public long AmountMinor { get; set; }
}
