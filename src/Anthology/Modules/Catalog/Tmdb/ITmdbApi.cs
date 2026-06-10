using Refit;

namespace Anthology.Modules.Catalog;

[Headers("Accept: application/json")]
public interface ITmdbApi
{
    [Get("/search/movie")]
    Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync([AliasAs("query")] string query, CancellationToken ct = default);

    [Get("/movie/{id}")]
    Task<TmdbMovie> GetMovieAsync(int id, CancellationToken ct = default);

    [Get("/movie/{id}?append_to_response=keywords,credits")]
    Task<TmdbMovieDetail> GetMovieDetailAsync(int id, CancellationToken ct = default);

    [Get("/search/tv")]
    Task<TmdbPagedResult<TmdbTvShow>> SearchTvAsync([AliasAs("query")] string query, CancellationToken ct = default);

    [Get("/tv/{id}")]
    Task<TmdbTvShow> GetTvShowAsync(int id, CancellationToken ct = default);

    [Get("/tv/{id}")]
    Task<TmdbTvShowDetail> GetTvShowDetailAsync(int id, CancellationToken ct = default);

    [Get("/tv/{id}/season/{seasonNumber}")]
    Task<TmdbSeason> GetSeasonAsync(int id, int seasonNumber, CancellationToken ct = default);
}
