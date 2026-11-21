using Microsoft.EntityFrameworkCore;
using Szamlazo.Application.Contracts;
using Szamlazo.Domain.Entities;

namespace Szamlazo.Infrastructure.Persistence;

internal sealed class EfLedgerStore(LedgerDb db) : ILedgerStore
{
    public async Task<IReadOnlyList<Expense>> LoadMonthAsync(string owner, DateOnly start, DateOnly end, CancellationToken token) =>
        await ForPeriod(owner, start, end).OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id).ToListAsync(token);

    public Task<long> TotalAsync(string owner, DateOnly start, DateOnly end, CancellationToken token) =>
        ForPeriod(owner, start, end).SumAsync(expense => expense.AmountMinor, token);

    public Task<long?> GetBudgetAsync(string owner, string month, CancellationToken token) =>
        db.Budgets.Where(budget => budget.OwnerId == owner && budget.Month == month)
            .Select(budget => (long?)budget.AmountMinor).SingleOrDefaultAsync(token);

    public async Task InsertNewAsync(string owner, IReadOnlyList<Expense> expenses, CancellationToken token)
    {
        var identifiers = expenses.Select(expense => expense.Id).ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(token);

        // The client retains these identifiers when retrying a save.
        var saved = await db.Expenses.Where(expense => expense.OwnerId == owner && identifiers.Contains(expense.Id))
            .Select(expense => expense.Id).ToHashSetAsync(token);
        db.Expenses.AddRange(expenses.Where(expense => !saved.Contains(expense.Id)));
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task<bool> UpdateAsync(Expense updated, CancellationToken token)
    {
        var expense = await db.Expenses.FindAsync([updated.OwnerId, updated.Id], token);
        if (expense is null)
            return false;

        expense.PaymentMethod = updated.PaymentMethod;
        expense.TransactionTime = updated.TransactionTime;
        expense.Merchant = updated.Merchant;
        expense.Date = updated.Date;
        expense.AmountMinor = updated.AmountMinor;
        expense.Category = updated.Category;
        expense.Items.Clear();
        expense.Items.AddRange(updated.Items);
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token)
    {
        var expense = await db.Expenses.FindAsync([owner, id], token);
        if (expense is null)
            return false;

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task SetBudgetAsync(string owner, string month, long? amountMinor, CancellationToken token)
    {
        if (amountMinor is null)
        {
            await db.Budgets.Where(budget => budget.OwnerId == owner && budget.Month == month).ExecuteDeleteAsync(token);
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Budgets (OwnerId, Month, AmountMinor) VALUES ({owner}, {month}, {amountMinor})
            ON CONFLICT (OwnerId, Month) DO UPDATE SET AmountMinor = excluded.AmountMinor
            """, token);
    }

    private IQueryable<Expense> ForPeriod(string owner, DateOnly start, DateOnly end) =>
        db.Expenses.AsNoTracking().Where(expense => expense.OwnerId == owner && expense.Date >= start && expense.Date < end);
}
