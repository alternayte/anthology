namespace Anthology.Modules.Catalog;

public sealed class TmdbProvider(ITmdbApi tmdb) : ICatalogProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Film, MediaType.TvShow }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public bool OwnsExternalId(string externalId) => externalId.StartsWith("tmdb-");

    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var movieTask = tmdb.SearchMoviesAsync(term, ct);
        var tvTask = tmdb.SearchTvAsync(term, ct);
        await Task.WhenAll(movieTask, tvTask);

        var results = new List<CatalogSearchResult>();
        results.AddRange(movieTask.Result.Results.Select(MapMovieResult));
        results.AddRange(tvTask.Result.Results.Select(MapTvResult));
        return results;
    }

    public async Task<Title?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        if (externalId.StartsWith("tmdb-tv-"))
            return await GetTvShowDetails(externalId, ct);

        return await GetFilmDetails(externalId, ct);
    }

    public static CatalogSearchResult MapMovieResult(TmdbMovie m) => new(
        $"tmdb-{m.Id}",
        MediaType.Film,
        m.Title,
        ParseYear(m.Release_Date),
        PosterUrl(m.Poster_Path),
        m.Overview);

    public static CatalogSearchResult MapTvResult(TmdbTvShow s) => new(
        $"tmdb-tv-{s.Id}",
        MediaType.TvShow,
        s.Name,
        ParseYear(s.First_Air_Date),
        PosterUrl(s.Poster_Path),
        s.Overview);

    private async Task<Title> GetFilmDetails(string externalId, CancellationToken ct)
    {
        var tmdbId = int.Parse(externalId.Replace("tmdb-", ""));
        var movie = await tmdb.GetMovieAsync(tmdbId, ct);

        return new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-{movie.Id}",
            MediaType = MediaType.Film,
            Name = movie.Title,
            Year = ParseYear(movie.Release_Date),
            PosterPath = PosterUrl(movie.Poster_Path),
            Overview = movie.Overview
        };
    }

    private async Task<Title> GetTvShowDetails(string externalId, CancellationToken ct)
    {
        var tmdbId = int.Parse(externalId.Replace("tmdb-tv-", ""));
        var show = await tmdb.GetTvShowAsync(tmdbId, ct);

        var showTitle = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-tv-{show.Id}",
            MediaType = MediaType.TvShow,
            Name = show.Name,
            Year = ParseYear(show.First_Air_Date),
            PosterPath = PosterUrl(show.Poster_Path),
            Overview = show.Overview
        };
        showTitle.SetMediaData(new TvShowData(show.Number_Of_Seasons, show.Number_Of_Episodes));

        return showTitle;
    }

    internal static int? ParseYear(string? date) =>
        DateTime.TryParse(date, out var d) ? d.Year : null;

    internal static string? PosterUrl(string? posterPath) =>
        posterPath is not null ? $"https://image.tmdb.org/t/p/w342{posterPath}" : null;
}
