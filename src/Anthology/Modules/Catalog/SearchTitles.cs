namespace Anthology.Modules.Catalog;

public static class SearchTitles
{
    public sealed record Query(string Term, MediaType MediaType = MediaType.Film);

    public sealed record TitleSearchResult(
        int TmdbId,
        string Name,
        int? Year,
        string? PosterPath,
        string? Overview);

    public sealed class Handler(ITmdbApi tmdb)
    {
        public async Task<IReadOnlyList<TitleSearchResult>> Handle(Query query, CancellationToken ct)
        {
            return query.MediaType switch
            {
                MediaType.TvShow => await SearchTv(query.Term, ct),
                _ => await SearchMovies(query.Term, ct)
            };
        }

        private async Task<IReadOnlyList<TitleSearchResult>> SearchMovies(string term, CancellationToken ct)
        {
            var result = await tmdb.SearchMoviesAsync(term, ct);
            return result.Results.Select(r => new TitleSearchResult(
                r.Id,
                r.Title,
                ParseYear(r.Release_Date),
                PosterUrl(r.Poster_Path),
                r.Overview
            )).ToList();
        }

        private async Task<IReadOnlyList<TitleSearchResult>> SearchTv(string term, CancellationToken ct)
        {
            var result = await tmdb.SearchTvAsync(term, ct);
            return result.Results.Select(r => new TitleSearchResult(
                r.Id,
                r.Name,
                ParseYear(r.First_Air_Date),
                PosterUrl(r.Poster_Path),
                r.Overview
            )).ToList();
        }

        internal static int? ParseYear(string? date) =>
            DateTime.TryParse(date, out var d) ? d.Year : null;

        internal static string? PosterUrl(string? posterPath) =>
            posterPath is not null ? $"https://image.tmdb.org/t/p/w342{posterPath}" : null;
    }
}
