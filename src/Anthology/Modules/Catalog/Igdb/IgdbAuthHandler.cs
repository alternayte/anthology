using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Anthology.Modules.Catalog;

public sealed class IgdbAuthHandler(IOptions<IgdbOptions> options) : DelegatingHandler
{
    private string? _token;
    private DateTimeOffset _expiresAt;
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        request.Headers.Remove("Client-ID");
        request.Headers.Add("Client-ID", options.Value.ClientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            using var http = new HttpClient();
            var response = await http.PostAsync(
                $"https://id.twitch.tv/oauth2/token?client_id={options.Value.ClientId}&client_secret={options.Value.ClientSecret}&grant_type=client_credentials",
                null, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(ct);
            _token = result!.Access_Token;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.Expires_In - 60);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record TwitchTokenResponse(
        [property: JsonPropertyName("access_token")] string Access_Token,
        [property: JsonPropertyName("expires_in")] int Expires_In);
}
