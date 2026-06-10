using System.Runtime.CompilerServices;
using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Anthology.Tests;

public sealed class CatalogSeederTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task SeedAsync_imports_titles_from_seedable_provider()
    {
        var (seeder, db, provider) = CreateSeeder("import-test", MediaType.Film, discoverCount: 3);

        await seeder.SeedAsync(new SeedCommandOptions(Count: 10), TestContext.Current.CancellationToken);

        var seeded = await db.Titles.AsNoTracking()
            .Where(t => t.ExternalId.StartsWith("import-test-"))
            .ToListAsync(TestContext.Current.CancellationToken);

        seeded.Should().HaveCount(3);
        seeded.Should().OnlyContain(t => t.MediaType == MediaType.Film);
    }

    [Fact]
    public async Task SeedAsync_respects_count_limit()
    {
        var (seeder, db, _) = CreateSeeder("count-test", MediaType.Film, discoverCount: 10);

        await seeder.SeedAsync(new SeedCommandOptions(Count: 3), TestContext.Current.CancellationToken);

        var seeded = await db.Titles.AsNoTracking()
            .Where(t => t.ExternalId.StartsWith("count-test-"))
            .CountAsync(TestContext.Current.CancellationToken);

        seeded.Should().Be(3);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_for_existing_titles()
    {
        var (seeder, db, _) = CreateSeeder("idempotent-test", MediaType.Film, discoverCount: 3);

        db.Titles.Add(new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = "idempotent-test-0",
            MediaType = MediaType.Film,
            Name = "Pre-existing Title"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await seeder.SeedAsync(new SeedCommandOptions(Count: 10), TestContext.Current.CancellationToken);

        var seeded = await db.Titles.AsNoTracking()
            .Where(t => t.ExternalId.StartsWith("idempotent-test-"))
            .ToListAsync(TestContext.Current.CancellationToken);

        seeded.Should().HaveCount(3);
        seeded.Should().ContainSingle(t => t.Name == "Pre-existing Title");
    }

    [Fact]
    public async Task SeedAsync_filters_by_provider_name()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var wanted = new FakeSeedableCatalogProvider("filter-wanted", MediaType.Film, 2, "tmdb");
        var skipped = new FakeSeedableCatalogProvider("filter-skipped", MediaType.Book, 2, "igdb");

        var handler = new AddTitle.Handler(db, [wanted, skipped], null!);
        var seeder = new CatalogSeeder([wanted, skipped], handler, NullLogger<CatalogSeeder>.Instance);

        await seeder.SeedAsync(
            new SeedCommandOptions(Count: 10, Providers: ["tmdb"]),
            TestContext.Current.CancellationToken);

        var wantedCount = await db.Titles.AsNoTracking()
            .Where(t => t.ExternalId.StartsWith("filter-wanted-"))
            .CountAsync(TestContext.Current.CancellationToken);
        var skippedCount = await db.Titles.AsNoTracking()
            .Where(t => t.ExternalId.StartsWith("filter-skipped-"))
            .CountAsync(TestContext.Current.CancellationToken);

        wantedCount.Should().Be(2);
        skippedCount.Should().Be(0);
    }

    private (CatalogSeeder seeder, CatalogDbContext db, FakeSeedableCatalogProvider provider) CreateSeeder(
        string prefix, MediaType mediaType, int discoverCount)
    {
        var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var provider = new FakeSeedableCatalogProvider(prefix, mediaType, discoverCount);
        var handler = new AddTitle.Handler(db, [provider], null!);
        var seeder = new CatalogSeeder([provider], handler, NullLogger<CatalogSeeder>.Instance);
        return (seeder, db, provider);
    }

    private sealed class FakeSeedableCatalogProvider(
        string prefix, MediaType mediaType, int discoverCount, string? providerName = null)
        : ICatalogProvider, ISeedableProvider
    {
        public string ProviderName => providerName ?? prefix;
        public IReadOnlySet<MediaType> SupportedTypes { get; } = new HashSet<MediaType> { mediaType }.AsReadOnly();
        public IReadOnlySet<MediaType> SeedableTypes => SupportedTypes;

        public bool OwnsExternalId(string externalId) => externalId.StartsWith($"{prefix}-");

        public Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogSearchResult>>([]);

        public Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct)
        {
            var title = new Title
            {
                TitleId = Guid.NewGuid(),
                ExternalId = externalId,
                MediaType = mediaType,
                Name = $"Title {externalId}",
                Year = 2020
            };
            return Task.FromResult<TitleWithCredits?>(new TitleWithCredits(title, []));
        }

        public async IAsyncEnumerable<CatalogSearchResult> DiscoverAsync(
            SeedOptions options, [EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 0; i < discoverCount; i++)
            {
                yield return new CatalogSearchResult(
                    $"{prefix}-{i}", mediaType, $"Title {i}", 2020, null, null);
                await Task.Yield();
            }
        }
    }
}
