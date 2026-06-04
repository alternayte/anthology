using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Anthology.Kernel.EventStore;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Tracking;
using Xunit;

namespace Anthology.Tests.Fixtures;

public sealed class WebAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ReplaceDbContext<EventStoreDbContext>(services);
                    ReplaceDbContext<IdentityDbContext>(services);
                    ReplaceDbContext<CatalogDbContext>(services);
                    ReplaceDbContext<TrackingDbContext>(services);
                    ReplaceDbContext<ProfileDbContext>(services);
                });
            });

        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<EventStoreDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<TrackingDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private void ReplaceDbContext<TContext>(IServiceCollection services) where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor is not null) services.Remove(descriptor);

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));
    }
}
