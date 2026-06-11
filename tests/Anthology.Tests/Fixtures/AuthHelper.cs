using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Anthology.Tests.Fixtures;

public static class AuthHelper
{
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        CancellationToken ct)
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var password = "TestPassword123";

        await client.PostAsJsonAsync("/api/identity/register",
            new { Email = email, Password = password }, ct);

        await client.PostAsJsonAsync("/api/identity/login",
            new { Email = email, Password = password }, ct);

        return client;
    }
}
