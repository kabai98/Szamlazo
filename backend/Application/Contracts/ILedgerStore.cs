using Szamlazo.Domain.Entities;

namespace Szamlazo.Application.Contracts;

public interface ILedgerStore
{
    Task<IReadOnlyList<Expense>> LoadMonthAsync(string owner, DateOnly start, DateOnly end, CancellationToken token);
    Task<long> TotalAsync(string owner, DateOnly start, DateOnly end, CancellationToken token);
    Task<long?> GetBudgetAsync(string owner, string month, CancellationToken token);
    Task InsertNewAsync(string owner, IReadOnlyList<Expense> expenses, CancellationToken token);
    Task<bool> UpdateAsync(Expense expense, CancellationToken token);
    Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token);
    Task SetBudgetAsync(string owner, string month, long? amountMinor, CancellationToken token);
}
