using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anthology.Kernel.EventStore;
using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class AdminEndpointTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
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

    private async Task<Guid> SeedTitleAndWantAsync(HttpClient client)
    {
        var ct = TestContext.Current.CancellationToken;
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
            Overview = "A test film for rebuild."
        };
        catalogDb.Titles.Add(title);
        await catalogDb.SaveChangesAsync(ct);

        await client.PostAsJsonAsync(
            $"/api/tracking/items/{title.TitleId}/want", new { }, ct);

        return title.TitleId;
    }

    [Fact]
    public async Task Rebuild_single_stream_returns_200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        await SeedTitleAndWantAsync(client);

        using var scope = fixture.Factory.Services.CreateScope();
        var esDb = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var stream = await esDb.Streams.AsNoTracking().FirstAsync(ct);

        var response = await client.PostAsync(
            $"/admin/streams/{stream.StreamId}/rebuild", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("eventsReplayed").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Rebuild_unknown_stream_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync(
            $"/admin/streams/{Guid.NewGuid()}/rebuild", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rebuild_unauthenticated_returns_401()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync(
            $"/admin/streams/{Guid.NewGuid()}/rebuild", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_rebuild_job_returns_202_with_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/admin/streams/rebuild",
            new { StreamType = "tracked_item" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/admin/streams/rebuild/");
    }

    [Fact]
    public async Task Create_rebuild_job_for_unknown_type_returns_422()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/admin/streams/rebuild",
            new { StreamType = "nonexistent" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Get_rebuild_job_status_returns_200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/admin/streams/rebuild",
            new { StreamType = "tracked_item" }, ct);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var jobId = createBody.GetProperty("jobId").GetGuid();

        var statusResponse = await client.GetAsync(
            $"/admin/streams/rebuild/{jobId}", ct);

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("streamType").GetString().Should().Be("tracked_item");
    }

    [Fact]
    public async Task Get_unknown_rebuild_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"/admin/streams/rebuild/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_stream_types_returns_registered_types()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/streams/types", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var types = Enumerable.Range(0, body.GetArrayLength())
            .Select(i => body[i].GetString())
            .ToList();
        types.Should().Contain("tracked_item");
    }
}
