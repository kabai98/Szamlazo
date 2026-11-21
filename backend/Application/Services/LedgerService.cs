using System.Globalization;
using Szamlazo.Application.Contracts;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain;
using Szamlazo.Domain.Entities;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Application.Services;

public sealed class LedgerService(ILedgerStore store)
{
    public async Task<DashboardDto> GetDashboardAsync(string owner, string? month, CancellationToken token)
    {
        if (!TryMonth(month, out var start))
            throw new LedgerRequestException("A hónap formátuma ÉÉÉÉ-HH.");

        var current = await store.LoadMonthAsync(owner, start, start.AddMonths(1), token);
        var previous = await store.TotalAsync(owner, start.AddMonths(-1), start, token);
        var budget = await store.GetBudgetAsync(owner, month!, token);
        var days = Enumerable.Range(0, DateTime.DaysInMonth(start.Year, start.Month))
            .GroupJoin(current, day => start.AddDays(day), expense => expense.Date,
                (day, group) => new DailySpending(day + 1, Money.FromMinor(group.Sum(expense => expense.AmountMinor))))
            .ToArray();

        var weekdays = Enumerable.Range(0, 7).Select(index =>
        {
            var weekday = (DayOfWeek)((index + 1) % 7);
            var matchingDays = days.Where(day => start.AddDays(day.Day - 1).DayOfWeek == weekday);
            return new WeekdaySpending(index, Math.Round(matchingDays.Average(day => day.Amount), 2));
        }).ToArray();

        var categories = current.GroupBy(expense => expense.Category)
            .Select(group => new CategorySpending(SpendingCategories.Label(group.Key),
                Money.FromMinor(group.Sum(expense => expense.AmountMinor))))
            .OrderByDescending(category => category.Amount)
            .ToArray();

        return new DashboardDto(month!, Money.FromMinor(current.Sum(expense => expense.AmountMinor)),
            Money.FromMinor(previous), budget.HasValue ? Money.FromMinor(budget.Value) : null, days, weekdays,
            categories, current.Select(ToDto).ToArray());
    }

    public Task SaveExpensesAsync(string owner, List<ExpenseInput?>? input, CancellationToken token)
    {
        if (input is not { Count: > 0 and <= 200 })
            throw new LedgerRequestException("Egyszerre 1–200 kiadás menthető.");

        var expenses = new List<Expense>();
        var ids = new HashSet<Guid>();
        foreach (var row in input)
        {
            if (row is null || !ids.Add(row.Id))
                throw InvalidExpense();

            expenses.Add(CreateExpense(row, owner, row.Id));
        }

        return store.InsertNewAsync(owner, expenses, token);
    }

    public async Task UpdateExpenseAsync(string owner, Guid id, ExpenseInput input, CancellationToken token)
    {
        var expense = CreateExpense(input, owner, id);
        if (!await store.UpdateAsync(expense, token))
            throw LedgerRequestException.NotFound();
    }

    public async Task DeleteExpenseAsync(string owner, Guid id, CancellationToken token)
    {
        if (!await store.DeleteAsync(owner, id, token))
            throw LedgerRequestException.NotFound();
    }

    public Task SetBudgetAsync(string owner, BudgetInput input, CancellationToken token)
    {
        if (!TryMonth(input.Month, out _) || input.Amount is { } amount && !Money.IsValid(amount, positive: true))
            throw new LedgerRequestException("Adj meg egy érvényes hónapot és pozitív forintösszeget.");

        return store.SetBudgetAsync(owner, input.Month!, input.Amount.HasValue ? Money.ToMinor(input.Amount.Value) : null, token);
    }

    private static Expense CreateExpense(ExpenseInput input, string owner, Guid id)
    {
        var merchant = input.Merchant?.Trim();
        var category = SpendingCategories.FromLabel(input.Category);
        if (id == Guid.Empty || merchant is not { Length: > 0 and <= 160 } ||
            input.Date is not { Year: >= 1900 and <= 2099 } date ||
            input.Amount is not { } amount || !Money.IsValid(amount, positive: true) ||
            category is null || input.Source is not { } source || !Enum.IsDefined(source) ||
            input.Items is { Count: > 100 })
            throw InvalidExpense();

        var items = new List<ExpenseItem>();
        foreach (var item in input.Items ?? [])
        {
            var name = item?.Name?.Trim();
            if (name is not { Length: > 0 and <= 160 } || item?.Amount is not { } price || !Money.IsValid(price, positive: false))
                throw InvalidExpense();

            if (item.Quantity is <= 0 or > 100000 || item.Unit is { Length: > 12 } ||
                item.UnitPrice is { } unitPrice && !Money.IsValid(unitPrice, positive: true))
                throw InvalidExpense();
            items.Add(new ExpenseItem { Name = name, AmountMinor = Money.ToMinor(price),
                Quantity = item.Quantity, Unit = item.Unit, UnitPrice = item.UnitPrice });
        }

        return new Expense
        {
            OwnerId = owner, Id = id, Merchant = merchant, Date = date, AmountMinor = Money.ToMinor(amount),
            Category = category.Value, Source = source, Items = items,
            PaymentMethod = input.PaymentMethod is { Length: <= 40 } ? input.PaymentMethod : null,
            TransactionTime = input.TransactionTime
        };
    }

    private static ExpenseDto ToDto(Expense expense) =>
        new(expense.Id, expense.Merchant, expense.Date, SpendingCategories.Label(expense.Category), expense.Source,
            Money.FromMinor(expense.AmountMinor), expense.Items.Select(item =>
                new ItemDto(item.Name, Money.FromMinor(item.AmountMinor), item.Quantity, item.Unit, item.UnitPrice)).ToArray(),
            expense.PaymentMethod, expense.TransactionTime);

    private static bool TryMonth(string? month, out DateOnly start) =>
        DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)
            && start.Year is >= 1900 and <= 2099;

    private static LedgerRequestException InvalidExpense() => new(
        "Ellenőrizd a nevet, dátumot, kategóriát és a pozitív forintösszeget. A tételekhez is szükséges név és összeg.");
}
