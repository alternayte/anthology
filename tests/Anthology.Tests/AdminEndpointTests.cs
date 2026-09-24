using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anthology.Tests.Fixtures;
using FluentAssertions;
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

    [Fact]
    public async Task Rebuild_unauthenticated_returns_401()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/admin/streams/rebuild",
            new { StreamType = "tracked_item" }, TestContext.Current.CancellationToken);

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

        for (var i = 0; i < 100 && body.GetProperty("status").GetString() != "done"; i++)
        {
            await Task.Delay(100, ct);
            body = await client.GetFromJsonAsync<JsonElement>($"/admin/streams/rebuild/{jobId}", ct);
        }

        body.GetProperty("status").GetString().Should().Be("done");
        body.GetProperty("progress").GetProperty("streams").GetInt32().Should().BeGreaterThan(0,
            "the migrated tracked items are rebuilt too");
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
