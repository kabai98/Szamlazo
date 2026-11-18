using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Szamlazo.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static async Task MigrateAsync(LedgerDb db, CancellationToken token = default)
    {
        await BaselineLegacySchemaAsync(db, token);
        await db.Database.MigrateAsync(token);
    }

    private static async Task BaselineLegacySchemaAsync(LedgerDb db, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        try
        {
            var existing = await ReadSchemaAsync(db.Database.GetDbConnection(), token);
            if (existing.Count == 0 || existing.Any(entry => entry.Name == "__EFMigrationsHistory"))
                return;

            // The first Docker build created this schema with EnsureCreated.
            // Accept it only when it exactly matches the checked-in initial migration.
            var migration = db.Database.GetMigrations().First();
            var script = db.GetService<IMigrator>().GenerateScript(toMigration: migration,
                options: MigrationsSqlGenerationOptions.NoTransactions);

            await using var expectedDb = new SqliteConnection("Data Source=:memory:");
            await expectedDb.OpenAsync(token);
            await using var command = expectedDb.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(token);

            var expected = await ReadSchemaAsync(expectedDb, token);
            if (!existing.SequenceEqual(expected.Where(entry => entry.Name != "__EFMigrationsHistory")))
                throw new InvalidOperationException("A meglévő adatbázis szerkezete eltér az első migrációétól. Az indítás leállt.");

            var history = db.GetService<IHistoryRepository>();
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            if (!(await db.Database.GetAppliedMigrationsAsync(token)).Any())
            {
                await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), token);
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, ProductInfo.GetVersion())), token);
            }
            await transaction.CommitAsync(token);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<List<SchemaEntry>> ReadSchemaAsync(DbConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, type, sql FROM sqlite_schema
            WHERE name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsLock' AND sql IS NOT NULL
            ORDER BY name, type;
            """;

        var entries = new List<SchemaEntry>();
        await using var rows = await command.ExecuteReaderAsync(token);
        while (await rows.ReadAsync(token))
            entries.Add(new SchemaEntry(rows.GetString(0), rows.GetString(1), Regex.Replace(rows.GetString(2), @"\s+", " ").Trim()));

        return entries;
    }

    private sealed record SchemaEntry(string Name, string Type, string Definition);
}
