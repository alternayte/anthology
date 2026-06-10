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

    [Get("/movie/popular")]
    Task<TmdbPagedResult<TmdbMovie>> GetPopularMoviesAsync([AliasAs("page")] int page, CancellationToken ct = default);

    [Get("/movie/top_rated")]
    Task<TmdbPagedResult<TmdbMovie>> GetTopRatedMoviesAsync([AliasAs("page")] int page, CancellationToken ct = default);

    [Get("/trending/movie/week")]
    Task<TmdbPagedResult<TmdbMovie>> GetTrendingMoviesAsync([AliasAs("page")] int page, CancellationToken ct = default);

    [Get("/tv/popular")]
    Task<TmdbPagedResult<TmdbTvShow>> GetPopularTvAsync([AliasAs("page")] int page, CancellationToken ct = default);

    [Get("/tv/top_rated")]
    Task<TmdbPagedResult<TmdbTvShow>> GetTopRatedTvAsync([AliasAs("page")] int page, CancellationToken ct = default);

    [Get("/trending/tv/week")]
    Task<TmdbPagedResult<TmdbTvShow>> GetTrendingTvAsync([AliasAs("page")] int page, CancellationToken ct = default);
}
