using Szamlazo.Domain.Enums;

namespace Szamlazo.Domain;

public static class ExpenseItemRules
{
    public static bool IsValidAmount(decimal amount, ExpenseItemKind kind) =>
        Money.IsValid(amount, positive: false) && kind switch
        {
            ExpenseItemKind.Product => amount >= 0,
            ExpenseItemKind.Discount => amount < 0,
            _ => false
        };
}
