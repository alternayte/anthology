using Deedbox;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Recommendations;
using Anthology.Modules.Tracking;
using Xunit;

namespace Anthology.Tests.Fixtures;

/// <summary>
/// The app on a database that starts as a copy of data written by the pre-Deedbox event store
/// (Fixtures/legacy-es.sql), moved into Deedbox by scripts/migrate-es-to-deedbox.sql.
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await RunScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-es.sql")));
        await RunScriptAsync(PostgresSchema.Script());
        await RunScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "migrate-es-to-deedbox.sql")));

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString()
                    });
                });
            });

        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<TrackingDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<RecommendationsDbContext>().Database.MigrateAsync();

        await WaitForProjectionsAsync(sp.GetRequiredService<IEventStoreAdmin>());
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>The migrated projections rebuild in the background; tests start once each one runs inline.</summary>
    private static async Task WaitForProjectionsAsync(IEventStoreAdmin admin)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var status = await admin.GetStatusAsync();
            if (status.Consumers.Count > 0 && status.Consumers.All(c => c.Status == "running"))
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Projections did not finish rebuilding: {string.Join(", ", status.Consumers)}");
            await Task.Delay(50);
        }
    }

    private async Task RunScriptAsync(string sql)
    {
        var result = await _container.ExecScriptAsync(sql);
        if (result.ExitCode != 0 || result.Stderr.Contains("ERROR", StringComparison.Ordinal))
            throw new InvalidOperationException($"Script failed: {result.Stderr}");
    }
}
