using Szamlazo.Domain.Enums;

namespace Szamlazo.Domain.Entities;

public sealed class ExpenseItem
{
    public ExpenseItemKind Kind { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public long AmountMinor { get; set; }
}
