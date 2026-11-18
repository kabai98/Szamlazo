using Szamlazo.Domain.Enums;

namespace Szamlazo.Domain.Entities;

public sealed class Expense
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Merchant { get; set; } = "";
    public DateOnly Date { get; set; }
    public long AmountMinor { get; set; }
    public SpendingCategory Category { get; set; }
    public ExpenseSource Source { get; set; }
    public List<ExpenseItem> Items { get; set; } = [];
}
