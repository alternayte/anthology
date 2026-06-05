namespace Anthology.Modules.Catalog;

public static class SearchTitles
{
    public sealed record Query(string Term);

    public sealed record TitleSearchResult(
        int TmdbId,
        string Name,
        int? Year,
        string? PosterPath,
        string? Overview);

    public sealed class Handler(TmdbClient tmdb)
    {
        public async Task<IReadOnlyList<TitleSearchResult>> Handle(Query query, CancellationToken ct)
        {
            var result = await tmdb.SearchMoviesAsync(query.Term, ct);
            return result.Results.Select(r => new TitleSearchResult(
                r.Id,
                r.Title,
                ParseYear(r.Release_Date),
                r.Poster_Path is not null ? $"https://image.tmdb.org/t/p/w342{r.Poster_Path}" : null,
                r.Overview
            )).ToList();
        }

        private static int? ParseYear(string? date) =>
            DateTime.TryParse(date, out var d) ? d.Year : null;
    }
}
