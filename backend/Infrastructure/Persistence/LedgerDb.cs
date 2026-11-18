using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Szamlazo.Domain.Entities;
using Szamlazo.Domain.Enums;
using Szamlazo.Infrastructure.Identity;

namespace Szamlazo.Infrastructure.Persistence;

public sealed class LedgerDb(DbContextOptions<LedgerDb> options) : IdentityDbContext<Account>(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        var expenses = model.Entity<Expense>();
        expenses.HasKey(expense => new { expense.OwnerId, expense.Id });
        expenses.HasIndex(expense => new { expense.OwnerId, expense.Date });
        expenses.Property(expense => expense.Category)
            .HasConversion(value => SpendingCategories.Label(value), label => SpendingCategories.Parse(label));
        expenses.Property(expense => expense.Source)
            .HasConversion(value => value.ToString().ToLowerInvariant(), label => Enum.Parse<ExpenseSource>(label, true));
        expenses.OwnsMany(expense => expense.Items, items =>
        {
            items.Property(item => item.Id).ValueGeneratedNever();
            items.WithOwner().HasForeignKey("OwnerId", "ExpenseId");
            items.HasKey("OwnerId", "ExpenseId", nameof(ExpenseItem.Id));
        });

        model.Entity<Budget>().HasKey(budget => new { budget.OwnerId, budget.Month });
    }
}
