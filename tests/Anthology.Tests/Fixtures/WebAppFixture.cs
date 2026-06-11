using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Anthology.Kernel.EventStore;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Recommendations;
using Anthology.Modules.Tracking;
using Xunit;

namespace Anthology.Tests.Fixtures;

public sealed class WebAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

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
        await sp.GetRequiredService<EventStoreDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<TrackingDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<RecommendationsDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}
