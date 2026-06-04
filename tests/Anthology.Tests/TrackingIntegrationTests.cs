using System.Net;
using System.Net.Http.Json;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Xunit;

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
}
