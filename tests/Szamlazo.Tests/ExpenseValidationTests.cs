using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Szamlazo.Application.Dtos;
using Szamlazo.Application.Services;
using Szamlazo.Domain;
using Szamlazo.Domain.Enums;
using Szamlazo.Infrastructure;
using Szamlazo.Infrastructure.Persistence;
using Xunit;

namespace Szamlazo.Tests;

public sealed class ExpenseValidationTests
{
    [Theory]
    [InlineData(-1, ExpenseItemKind.Product, false)]
    [InlineData(0, ExpenseItemKind.Product, true)]
    [InlineData(12.50, ExpenseItemKind.Product, true)]
    [InlineData(-100, ExpenseItemKind.Discount, true)]
    [InlineData(0, ExpenseItemKind.Discount, false)]
    [InlineData(100, ExpenseItemKind.Discount, false)]
    [InlineData(10, (ExpenseItemKind)99, false)]
    [InlineData(1.001, ExpenseItemKind.Product, false)]
    public void Amount_obeys_item_kind(decimal amount, ExpenseItemKind kind, bool expected)
    {
        Assert.Equal(expected, ExpenseItemRules.IsValidAmount(amount, kind));
    }

    [Fact]
    public async Task Service_rejects_negative_product_on_create_and_update()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var valid = Input([new("Tej", 500)]);
        await fixture.Service.SaveExpensesAsync("owner", [valid], default);
        var invalid = valid with { Items = [new("Tej", -500)] };
        await Assert.ThrowsAsync<LedgerRequestException>(() =>
            fixture.Service.SaveExpensesAsync("owner", [invalid with { Id = Guid.NewGuid() }], default));
        await Assert.ThrowsAsync<LedgerRequestException>(() =>
            fixture.Service.UpdateExpenseAsync("owner", valid.Id, invalid, default));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(50000, (await fixture.Db.Expenses.SingleAsync()).Items.Single().AmountMinor);
    }

    [Fact]
    public async Task Discount_survives_reload_and_is_scoped_to_owner()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await fixture.Service.SaveExpensesAsync("owner", [Input([
            new("Tej", 500), new("Akció", -100, Kind: ExpenseItemKind.Discount)])], default);
        fixture.Db.ChangeTracker.Clear();
        var report = await fixture.Service.GetDashboardAsync("owner", "2026-11", default);
        Assert.Equal(ExpenseItemKind.Discount, report.Expenses.Single().Items.Single(i => i.Amount < 0).Kind);
        Assert.Empty((await fixture.Service.GetDashboardAsync("other", "2026-11", default)).Expenses);
    }

    private static ExpenseInput Input(List<ItemInput?> items) =>
        new(Guid.NewGuid(), "Bolt", new DateOnly(2026, 11, 1), 400, "Élelmiszer", ExpenseSource.Manual, items);

    [Fact]
    public async Task Migration_preserves_existing_negative_items_as_discounts()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var migrator = fixture.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        var previous = fixture.Db.Database.GetMigrations().Reverse().Skip(1).First();
        await migrator.MigrateAsync(previous);
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Expenses (OwnerId, Id, Merchant, Date, AmountMinor, Category, Source)
            VALUES ('legacy', '00000000-0000-0000-0000-000000000001', 'Bolt', '2026-11-01', 10000, 'Élelmiszer', 'manual');
            INSERT INTO ExpenseItem (OwnerId, ExpenseId, Id, Name, AmountMinor)
            VALUES ('legacy', '00000000-0000-0000-0000-000000000001',
                '00000000-0000-0000-0000-000000000002', 'Régi akció', -1000);
            """);
        await DatabaseSetup.MigrateAsync(fixture.Db);
        var item = (await fixture.Db.Expenses.SingleAsync()).Items.Single();
        Assert.Equal(-1000, item.AmountMinor);
        Assert.Equal(ExpenseItemKind.Discount, item.Kind);
    }

    private sealed class DatabaseFixture(ServiceProvider provider, string directory) : IAsyncDisposable
    {
        public LedgerDb Db => provider.GetRequiredService<LedgerDb>();
        public LedgerService Service => provider.GetRequiredService<LedgerService>();

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var directory = Directory.CreateTempSubdirectory("szamlazo-tests-").FullName;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(directory);
            services.AddScoped<LedgerService>();
            var fixture = new DatabaseFixture(services.BuildServiceProvider(), directory);
            await DatabaseSetup.MigrateAsync(fixture.Db);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
