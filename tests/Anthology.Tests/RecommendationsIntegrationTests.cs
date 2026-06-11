using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthology.Modules.Catalog;
using Anthology.Modules.Recommendations;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Xunit;

namespace Anthology.Tests;

public sealed class RecommendationsIntegrationTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
    private const string EmbeddingModel = "text-embedding-3-small";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private Task<HttpClient> CreateAuthenticatedClientAsync() =>
        AuthHelper.CreateAuthenticatedClientAsync(fixture.Factory, TestContext.Current.CancellationToken);

    private static Vector Embedding(params (int index, float value)[] entries)
    {
        var emb = new float[1536];
        foreach (var (index, value) in entries)
            emb[index] = value;
        return new Vector(emb);
    }

    private async Task<Guid> SeedTitleAsync(
        string name, double popularity, Vector embedding, int? year = 2000)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"rec-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = name,
            Year = year,
            PosterPath = "/poster.jpg",
            Genres = ["Drama"],
            Keywords = ["test"],
            Popularity = popularity,
            Embedding = embedding,
            EmbeddingModel = EmbeddingModel
        };
        catalogDb.Titles.Add(title);
        await catalogDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.TitleId;
    }

    // Legal tracking sequence found in TrackedItem.Decide:
    // want (None -> WantToConsume) -> start (-> InProgress) -> finish (-> Finished) -> rate.
    // Rate is rejected for None/WantToConsume, so it must follow start/finish.
    // Rating sets LibraryItem.Rating, which makes the title a seed when >= 8.
    private async Task RateAsync(HttpClient client, Guid titleId, int rating)
    {
        var ct = TestContext.Current.CancellationToken;
        (await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/want", new { }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/start", new { }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/finish", new { }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/rate", new { rating }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<List<GetForYou.FeedRowDto>> GetForYouAsync(HttpClient client)
    {
        var rows = await client.GetFromJsonAsync<List<GetForYou.FeedRowDto>>(
            "/api/recommendations/for-you", JsonOptions, TestContext.Current.CancellationToken);
        return rows ?? [];
    }

    // Seeds three Films with mutually-orthogonal embeddings (one-hot => cosine distance 1.0,
    // far above the 0.15 diversity threshold). Each gets a distinct close "neighbour":
    // value 1.0 at the same index + a tiny 0.02 at a unique high index, so the neighbour is the
    // nearest non-seed catalog title to its seed. Rates all three seeds 9, then returns the IDs.
    private async Task<(Guid[] seedIds, Guid[] neighbourIds)> SetupPersonalizedAsync(HttpClient client)
    {
        var seedA = await SeedTitleAsync("Seed A", 10, Embedding((0, 1f)));
        var seedB = await SeedTitleAsync("Seed B", 10, Embedding((1, 1f)));
        var seedC = await SeedTitleAsync("Seed C", 10, Embedding((2, 1f)));

        var neighbourA = await SeedTitleAsync("Neighbour A", 5, Embedding((0, 1f), (100, 0.02f)));
        var neighbourB = await SeedTitleAsync("Neighbour B", 5, Embedding((1, 1f), (101, 0.02f)));
        var neighbourC = await SeedTitleAsync("Neighbour C", 5, Embedding((2, 1f), (102, 0.02f)));

        await RateAsync(client, seedA, 9);
        await RateAsync(client, seedB, 9);
        await RateAsync(client, seedC, 9);

        return ([seedA, seedB, seedC], [neighbourA, neighbourB, neighbourC]);
    }

    [Fact]
    public async Task ForYou_requires_authentication()
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/recommendations/for-you",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForYou_falls_back_to_popular_when_user_has_no_seeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var popularId = await SeedTitleAsync("Hugely Popular Film", 1_000_000_000d, Embedding((500, 1f)));

        var rows = await GetForYouAsync(client);

        rows.Should().ContainSingle();
        rows[0].SeedName.Should().Be("Popular right now");
        rows[0].Items.Should().Contain(i => i.TitleId == popularId);
    }

    [Fact]
    public async Task ForYou_returns_personalized_rows_seeded_by_highly_rated_titles()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (seedIds, neighbourIds) = await SetupPersonalizedAsync(client);

        var rows = await GetForYouAsync(client);

        rows.Should().NotContain(r => r.SeedName == "Popular right now",
            "with 3 distant seeds the feed should be personalized, not cold-start");
        rows.Should().Contain(r => seedIds.Contains(r.SeedTitleId),
            "at least one row should be seeded by a highly-rated title");

        var itemIds = rows.SelectMany(r => r.Items).Select(i => i.TitleId).ToHashSet();

        itemIds.Should().Contain(id => neighbourIds.Contains(id),
            "a known close neighbour of a seed should appear in the feed");
        itemIds.Should().NotContain(id => seedIds.Contains(id),
            "seen (rated) seed titles must never appear in the feed");
    }

    [Fact]
    public async Task MoreLikeThis_promotes_an_unrated_title_to_a_seed_row()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ct = TestContext.Current.CancellationToken;

        // Arrange: two rated seeds with mutually-distant one-hot embeddings (cosine distance = 1.0).
        var ratedA = await SeedTitleAsync("Rated A", 10, Embedding((0, 1f)));
        var ratedB = await SeedTitleAsync("Rated B", 10, Embedding((1, 1f)));
        await RateAsync(client, ratedA, 9);
        await RateAsync(client, ratedB, 9);

        // Third seed: "promoted" title — the user never rates it; it becomes a seed only via MoreLikeThis.
        var promoted = await SeedTitleAsync("Promoted Unrated", 10, Embedding((2, 1f)));

        // Neighbour of the promoted title: very close (same primary dimension + tiny offset so the
        // embedding is non-identical) so it wins the nearest-neighbour search for the promoted seed.
        var promotedNeighbour = await SeedTitleAsync("Promoted Neighbour", 5, Embedding((2, 1f), (103, 0.02f)));

        // Submit MoreLikeThis feedback for the unrated promoted title — this is the signal under test.
        (await client.PostAsJsonAsync("/api/recommendations/feedback",
            new { titleId = promoted, signal = "more_like_this" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Act.
        var rows = await GetForYouAsync(client);

        // Assert: 3 seeds (2 rated + 1 promoted) → personalised feed, no cold-start row.
        rows.Should().NotContain(r => r.SeedName == "Popular right now",
            "3 seeds (2 rated + 1 promoted) should trigger the personalized feed, not cold-start");

        // A row seeded by the promoted (unrated) title must exist.
        rows.Should().Contain(r => r.SeedTitleId == promoted,
            "MoreLikeThis feedback should promote an unrated title to a seed row");

        // That row's items must include the promoted title's nearest neighbour.
        var promotedRow = rows.First(r => r.SeedTitleId == promoted);
        promotedRow.Items.Should().Contain(i => i.TitleId == promotedNeighbour,
            "the promoted seed's row should contain its nearest catalogue neighbour");

        // The promoted title itself must NOT appear in any row's items — FindSimilarTitles excludes the
        // source seed from its own results, and the promoted title is not in the library so it is also
        // not in seenIds, but it is always excluded as the seed itself.
        var allItemIds = rows.SelectMany(r => r.Items).Select(i => i.TitleId).ToHashSet();
        allItemIds.Should().NotContain(promoted,
            "the promoted seed title must never appear as a recommendation item in its own row");
    }

    [Fact]
    public async Task Hidden_feedback_excludes_a_title_and_restore_brings_it_back()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, neighbourIds) = await SetupPersonalizedAsync(client);
        var ct = TestContext.Current.CancellationToken;

        var rowsBefore = await GetForYouAsync(client);
        var feedItemIds = rowsBefore.SelectMany(r => r.Items).Select(i => i.TitleId).ToList();
        var visibleNeighbour = feedItemIds.FirstOrDefault(id => neighbourIds.Contains(id));
        visibleNeighbour.Should().NotBe(Guid.Empty, "a seeded neighbour should appear in the personalized feed before hiding");

        // Hide it.
        (await client.PostAsJsonAsync("/api/recommendations/feedback",
            new { titleId = visibleNeighbour, signal = "hidden" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var rowsHidden = await GetForYouAsync(client);
        rowsHidden.SelectMany(r => r.Items).Select(i => i.TitleId)
            .Should().NotContain(visibleNeighbour, "a hidden title must not appear in the feed");

        var hidden = await client.GetFromJsonAsync<List<GetHiddenTitles.HiddenTitleDto>>(
            "/api/recommendations/hidden", ct);
        hidden!.Select(h => h.TitleId).Should().Contain(visibleNeighbour);

        // Restore it.
        (await client.PostAsJsonAsync("/api/recommendations/feedback",
            new { titleId = visibleNeighbour, signal = "restored" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var rowsRestored = await GetForYouAsync(client);
        rowsRestored.SelectMany(r => r.Items).Select(i => i.TitleId)
            .Should().Contain(visibleNeighbour, "a restored title should reappear in the feed");

        var hiddenAfter = await client.GetFromJsonAsync<List<GetHiddenTitles.HiddenTitleDto>>(
            "/api/recommendations/hidden", ct);
        hiddenAfter!.Select(h => h.TitleId).Should().NotContain(visibleNeighbour);
    }
}
