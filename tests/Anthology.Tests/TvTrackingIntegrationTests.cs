using System.Net;
using System.Net.Http.Json;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class TvTrackingIntegrationTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var password = "TestPassword123";
        await client.PostAsJsonAsync("/api/identity/register",
            new { Email = email, Password = password }, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync("/api/identity/login",
            new { Email = email, Password = password }, TestContext.Current.CancellationToken);
        return client;
    }

    private async Task<(Guid ShowId, Guid Episode1Id, Guid Episode2Id)> SeedTvShowAsync(string showName = "Breaking Bad")
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var show = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-show-{Guid.NewGuid():N}",
            MediaType = MediaType.TvShow,
            Name = showName,
            Year = 2008,
            Overview = "A chemistry teacher becomes a drug lord."
        };
        catalogDb.Titles.Add(show);

        var season = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-season-{Guid.NewGuid():N}",
            MediaType = MediaType.Season,
            Name = $"{showName} Season 1",
            Year = 2008,
            ParentTitleId = show.TitleId,
            SortOrder = 1
        };
        catalogDb.Titles.Add(season);

        var episode1 = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-ep1-{Guid.NewGuid():N}",
            MediaType = MediaType.Episode,
            Name = "Pilot",
            Year = 2008,
            ParentTitleId = season.TitleId,
            SortOrder = 1
        };
        catalogDb.Titles.Add(episode1);

        var episode2 = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-ep2-{Guid.NewGuid():N}",
            MediaType = MediaType.Episode,
            Name = "Cat's in the Bag",
            Year = 2008,
            ParentTitleId = season.TitleId,
            SortOrder = 2
        };
        catalogDb.Titles.Add(episode2);

        await catalogDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (show.TitleId, episode1.TitleId, episode2.TitleId);
    }

    [Fact]
    public async Task Tracking_an_episode_creates_show_summary_row()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (showId, episode1Id, _) = await SeedTvShowAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tracking/items/{episode1Id}/want", new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var showRow = await trackingDb.LibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(li => li.TitleId == showId, TestContext.Current.CancellationToken);

        showRow.Should().NotBeNull();
        showRow!.MediaType.Should().Be(MediaType.TvShow);
        showRow.PartsTotal.Should().Be(2);
        showRow.PartsCompleted.Should().Be(0);
        showRow.Status.Should().Be(TrackedStatus.WantToConsume);
    }

    [Fact]
    public async Task Finishing_an_episode_increments_parts_completed_and_sets_show_to_in_progress()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (showId, episode1Id, _) = await SeedTvShowAsync();

        await client.PostAsJsonAsync(
            $"/api/tracking/items/{episode1Id}/want", new { },
            TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync(
            $"/api/tracking/items/{episode1Id}/start", new { },
            TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync(
            $"/api/tracking/items/{episode1Id}/finish", new { Rating = (int?)null },
            TestContext.Current.CancellationToken);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var showRow = await trackingDb.LibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(li => li.TitleId == showId, TestContext.Current.CancellationToken);

        showRow.Should().NotBeNull();
        showRow!.PartsCompleted.Should().Be(1);
        showRow.PartsTotal.Should().Be(2);
        showRow.Status.Should().Be(TrackedStatus.InProgress);
    }

    [Fact]
    public async Task Finishing_all_episodes_marks_show_as_finished()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (showId, episode1Id, episode2Id) = await SeedTvShowAsync();

        foreach (var episodeId in new[] { episode1Id, episode2Id })
        {
            await client.PostAsJsonAsync(
                $"/api/tracking/items/{episodeId}/want", new { },
                TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync(
                $"/api/tracking/items/{episodeId}/start", new { },
                TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync(
                $"/api/tracking/items/{episodeId}/finish", new { Rating = (int?)null },
                TestContext.Current.CancellationToken);
        }

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var showRow = await trackingDb.LibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(li => li.TitleId == showId, TestContext.Current.CancellationToken);

        showRow.Should().NotBeNull();
        showRow!.PartsCompleted.Should().Be(2);
        showRow.PartsTotal.Should().Be(2);
        showRow.Status.Should().Be(TrackedStatus.Finished);
    }
}
