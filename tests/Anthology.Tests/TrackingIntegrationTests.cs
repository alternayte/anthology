using System.Net;
using System.Net.Http.Json;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;

namespace Anthology.Tests;

public sealed class TrackingIntegrationTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task Unauthenticated_tracking_request_returns_401()
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/tracking/items/{Guid.NewGuid()}/want", new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Identity_me_unauthenticated_returns_401()
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/identity/me",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync() =>
        AuthHelper.CreateAuthenticatedClientAsync(fixture.Factory, TestContext.Current.CancellationToken);

    private async Task<Guid> SeedTitleAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "The Matrix",
            Year = 1999,
            PosterPath = "/poster.jpg",
            Overview = "A computer hacker learns about the true nature of reality."
        };
        catalogDb.Titles.Add(title);
        await catalogDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.TitleId;
    }

    [Fact]
    public async Task Want_item_persists_library_projection()
    {
        var client = await CreateAuthenticatedClientAsync();
        var titleId = await SeedTitleAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tracking/items/{titleId}/want", new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var libraryItem = await trackingDb.LibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.TitleId == titleId,
                TestContext.Current.CancellationToken);

        libraryItem.Should().NotBeNull();
        libraryItem!.Title.Should().Be("The Matrix");
        libraryItem.MediaType.Should().Be(MediaType.Film);
        libraryItem.Status.Should().Be(TrackedStatus.WantToConsume);
    }

    [Fact]
    public async Task Want_item_persists_diary_projection()
    {
        var client = await CreateAuthenticatedClientAsync();
        var titleId = await SeedTitleAsync();

        await client.PostAsJsonAsync(
            $"/api/tracking/items/{titleId}/want", new { },
            TestContext.Current.CancellationToken);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var diaryEntry = await trackingDb.DiaryEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TitleId == titleId,
                TestContext.Current.CancellationToken);

        diaryEntry.Should().NotBeNull();
        diaryEntry!.Status.Should().Be(TrackedStatus.WantToConsume);
    }

    [Fact]
    public async Task Want_nonexistent_title_returns_404()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tracking/items/{Guid.NewGuid()}/want", new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
