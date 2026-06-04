using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Anthology.Modules.Catalog;

public sealed class TmdbClient(HttpClient http, IOptions<TmdbOptions> options)
{
    private readonly string _apiKey = options.Value.ApiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";

    public async Task<TmdbSearchResult> SearchMoviesAsync(string query, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";
        return await http.GetFromJsonAsync<TmdbSearchResult>(url, ct)
            ?? new TmdbSearchResult([]);
    }

    public async Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/movie/{tmdbId}?api_key={_apiKey}";
        return await http.GetFromJsonAsync<TmdbMovie>(url, ct);
    }

    public sealed record TmdbSearchResult(List<TmdbMovieSearchItem> Results);

    public sealed record TmdbMovieSearchItem(
        int Id,
        string Title,
        string? Overview,
        string? Release_Date,
        string? Poster_Path);

    public sealed record TmdbMovie(
        int Id,
        string Title,
        string? Overview,
        string? Release_Date,
        string? Poster_Path);
}
