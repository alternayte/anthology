using System.Net.Http.Json;
using System.Text;

namespace Anthology.Modules.Catalog;

public sealed class IgdbClient(HttpClient http)
{
    public async Task<List<IgdbGame>> SearchGamesAsync(string query, CancellationToken ct)
    {
        var body = $"""search "{query}"; fields name,first_release_date,summary,cover.image_id,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,platforms.name,genres.name,themes.name,keywords.name,total_rating,total_rating_count; limit 20;""";
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await http.PostAsync("games", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: ct) ?? [];
    }

    public async Task<IgdbGame?> GetGameAsync(int id, CancellationToken ct)
    {
        var body = $"fields name,first_release_date,summary,cover.image_id,artworks.image_id,screenshots.image_id,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,platforms.name,genres.name,themes.name,keywords.name,total_rating,total_rating_count; where id = {id};";
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await http.PostAsync("games", content, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: ct) ?? [];
        return results.FirstOrDefault();
    }

    public async Task<List<IgdbGame>> DiscoverGamesAsync(string body, CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await http.PostAsync("games", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: ct) ?? [];
    }
}
