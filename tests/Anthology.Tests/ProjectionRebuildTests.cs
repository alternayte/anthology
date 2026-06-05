using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class ProjectionRebuildTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
    private static string Snake<T>(T v) where T : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString());

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var password = "TestPassword123";

        await client.PostAsJsonAsync("/api/identity/register",
            new { Email = email, Password = password },
            TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync("/api/identity/login",
            new { Email = email, Password = password },
            TestContext.Current.CancellationToken);

        return client;
    }

    private async Task<Guid> SeedTitleAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Rebuild Test Film",
            Year = 2024,
            PosterPath = "/poster.jpg",
            Overview = "A test film for projection rebuild."
        };
        catalogDb.Titles.Add(title);
        await catalogDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.TitleId;
    }

    [Fact]
    public async Task Diary_projection_insert_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var titleId = await SeedTitleAsync();

        await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/want", new { }, ct);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var entry = await trackingDb.DiaryEntries.AsNoTracking()
            .FirstAsync(e => e.TitleId == titleId, ct);

        var affected = await trackingDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tracking.diary_entries (user_id, title_id, status, rating, occurred_at, visibility)
            VALUES ({entry.UserId}, {entry.TitleId}, {Snake(entry.Status)}, {entry.Rating}, {entry.OccurredAt}, {Snake(entry.Visibility)})
            ON CONFLICT (user_id, title_id, occurred_at) DO NOTHING
            """, ct);

        affected.Should().Be(0, "idempotent insert should not insert a duplicate row");

        var count = await trackingDb.DiaryEntries.AsNoTracking()
            .CountAsync(e => e.TitleId == titleId, ct);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Library_projection_insert_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var titleId = await SeedTitleAsync();

        await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/want", new { }, ct);

        using var scope = fixture.Factory.Services.CreateScope();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var item = await trackingDb.LibraryItems.AsNoTracking()
            .FirstAsync(i => i.TitleId == titleId, ct);

        var affected = await trackingDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tracking.library_items (user_id, title_id, media_type, title, status, added_at, visibility)
            VALUES ({item.UserId}, {item.TitleId}, {Snake(item.MediaType)}, {item.Title}, {Snake(item.Status)}, {item.AddedAt}, {Snake(item.Visibility)})
            ON CONFLICT (user_id, title_id) DO NOTHING
            """, ct);

        affected.Should().Be(0, "idempotent insert should not insert a duplicate row");

        var count = await trackingDb.LibraryItems.AsNoTracking()
            .CountAsync(i => i.TitleId == titleId, ct);
        count.Should().Be(1);
    }

    [Fact]
    public async Task List_projections_returns_registered_projections()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/projections", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var names = Enumerable.Range(0, body.GetArrayLength())
            .Select(i => body[i].GetProperty("projection").GetString())
            .ToList();
        names.Should().Contain("DiaryProjection");
        names.Should().Contain("LibraryProjection");
    }

    [Fact]
    public async Task Get_projection_status_returns_status()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/projections/DiaryProjection/status", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("projection").GetString().Should().Be("DiaryProjection");
        body.GetProperty("position").GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Get_unknown_projection_status_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/projections/NonExistent/status", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rebuild_projection_returns_202()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var titleId = await SeedTitleAsync();

        await client.PostAsJsonAsync($"/api/tracking/items/{titleId}/want", new { }, ct);

        var response = await client.PostAsync("/admin/projections/DiaryProjection/rebuild", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Rebuild_unknown_projection_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/admin/projections/NonExistent/rebuild", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rebuild_unauthenticated_returns_401()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync("/admin/projections/DiaryProjection/rebuild", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
