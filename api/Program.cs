using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Szamlazo.Api.Controllers;
using Szamlazo.Api.Errors;
using Szamlazo.Api.Security;
using Szamlazo.Application.Services;
using Szamlazo.Infrastructure;
using Szamlazo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var storage = Path.GetFullPath(builder.Configuration["Storage:Directory"] ?? "data", builder.Environment.ContentRootPath);
Directory.CreateDirectory(storage);
var keys = Directory.CreateDirectory(Path.Combine(storage, "keys"));

builder.Services.AddInfrastructure(storage);
builder.Services.AddScoped<LedgerService>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(keys).SetApplicationName("Szamlazo");
builder.Services.AddAuthorization();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Szamlazo.Session";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-Szamlazo-Csrf";
    options.Cookie.Name = "Szamlazo.Csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddControllers(options => options.Filters.Add<CsrfGuard>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = ImportsController.MaxBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = ImportsController.MaxBytes + 65536);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    options.AddConcurrencyLimiter("import", limiter => limiter.PermitLimit = 2);
});
builder.Services.AddExceptionHandler<LedgerExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseSetup.MigrateAsync(scope.ServiceProvider.GetRequiredService<LedgerDb>());
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
