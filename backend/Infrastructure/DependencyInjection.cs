using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Szamlazo.Application.Contracts;
using Szamlazo.Infrastructure.Identity;
using Szamlazo.Infrastructure.Importing;
using Szamlazo.Infrastructure.Persistence;

namespace Szamlazo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string storage)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(storage, "ledger.db") }.ToString();
        services.AddDbContext<LedgerDb>(options => options.UseSqlite(connectionString));
        services.AddScoped<ILedgerStore, EfLedgerStore>();
        services.AddSingleton<IDocumentReader, DocumentReader>();
        services.AddIdentity<Account, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
        }).AddEntityFrameworkStores<LedgerDb>();
        return services;
    }
}
