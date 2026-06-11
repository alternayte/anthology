using System.Runtime.CompilerServices;

namespace Anthology.Modules.Catalog;

public sealed class TmdbProvider(ITmdbApi tmdb) : ICatalogProvider, ISeedableProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Film, MediaType.TvShow }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public string ProviderName => "tmdb";

    public async IAsyncEnumerable<CatalogSearchResult> DiscoverAsync(
        SeedOptions options, [EnumeratorCancellation] CancellationToken ct)
    {
        var seen = new HashSet<string>();
        var yielded = 0;

        foreach (var list in options.Lists)
        {
            if (yielded >= options.Count) break;

            await foreach (var result in DiscoverListAsync(list, options.Count - yielded, ct))
            {
                if (yielded >= options.Count) break;
                if (seen.Add(result.ExternalId))
                {
                    yielded++;
                    yield return result;
                }
            }
        }
    }

    private async IAsyncEnumerable<CatalogSearchResult> DiscoverListAsync(
        string list, int remaining, [EnumeratorCancellation] CancellationToken ct)
    {
        var movieFetcher = GetMovieFetcher(list);
        var tvFetcher = GetTvFetcher(list);

        if (movieFetcher is not null)
        {
            await foreach (var result in FetchMoviePagesAsync(movieFetcher, remaining, ct))
                yield return result;
        }

        if (tvFetcher is not null)
        {
            await foreach (var result in FetchTvPagesAsync(tvFetcher, remaining, ct))
                yield return result;
        }
    }

    private Func<int, CancellationToken, Task<TmdbPagedResult<TmdbMovie>>>? GetMovieFetcher(string list) =>
        list switch
        {
            "popular" => tmdb.GetPopularMoviesAsync,
            "top_rated" => tmdb.GetTopRatedMoviesAsync,
            "trending" => tmdb.GetTrendingMoviesAsync,
            _ => null
        };

    private Func<int, CancellationToken, Task<TmdbPagedResult<TmdbTvShow>>>? GetTvFetcher(string list) =>
        list switch
        {
            "popular" => tmdb.GetPopularTvAsync,
            "top_rated" => tmdb.GetTopRatedTvAsync,
            "trending" => tmdb.GetTrendingTvAsync,
            _ => null
        };

    private static async IAsyncEnumerable<CatalogSearchResult> FetchMoviePagesAsync(
        Func<int, CancellationToken, Task<TmdbPagedResult<TmdbMovie>>> fetch,
        int remaining, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = 0;
        for (var page = 1; yielded < remaining; page++)
        {
            var result = await fetch(page, ct);
            if (result.Results.Count == 0) break;

            foreach (var movie in result.Results)
            {
                if (yielded >= remaining) break;
                yielded++;
                yield return MapMovieResult(movie);
            }

            if (page >= result.Total_Pages) break;
        }
    }

    private static async IAsyncEnumerable<CatalogSearchResult> FetchTvPagesAsync(
        Func<int, CancellationToken, Task<TmdbPagedResult<TmdbTvShow>>> fetch,
        int remaining, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = 0;
        for (var page = 1; yielded < remaining; page++)
        {
            var result = await fetch(page, ct);
            if (result.Results.Count == 0) break;

            foreach (var show in result.Results)
            {
                if (yielded >= remaining) break;
                yielded++;
                yield return MapTvResult(show);
            }

            if (page >= result.Total_Pages) break;
        }
    }

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

    public async Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct)
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

    private async Task<TitleWithCredits> GetFilmDetails(string externalId, CancellationToken ct)
    {
        var tmdbId = int.Parse(externalId.Replace("tmdb-", ""));
        var movie = await tmdb.GetMovieDetailAsync(tmdbId, ct);

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-{movie.Id}",
            MediaType = MediaType.Film,
            Name = movie.Title,
            Year = ParseYear(movie.Release_Date),
            PosterPath = PosterUrl(movie.Poster_Path),
            BackdropPath = BackdropUrl(movie.Backdrop_Path),
            Overview = movie.Overview,
            Genres = movie.Genres.Select(g => g.Name).ToArray(),
            Keywords = movie.Keywords.Keywords.Select(k => k.Name).ToArray(),
            Popularity = movie.Popularity,
            VoteAverage = movie.Vote_Average
        };

        var credits = BuildCredits(title.TitleId, movie.Credits);
        return new TitleWithCredits(title, credits);
    }

    private async Task<TitleWithCredits> GetTvShowDetails(string externalId, CancellationToken ct)
    {
        var tmdbId = int.Parse(externalId.Replace("tmdb-tv-", ""));
        var show = await tmdb.GetTvShowDetailAsync(tmdbId, ct);

        var showTitle = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-tv-{show.Id}",
            MediaType = MediaType.TvShow,
            Name = show.Name,
            Year = ParseYear(show.First_Air_Date),
            PosterPath = PosterUrl(show.Poster_Path),
            BackdropPath = BackdropUrl(show.Backdrop_Path),
            Overview = show.Overview,
            Genres = show.Genres.Select(g => g.Name).ToArray(),
            Keywords = [],
            Popularity = show.Popularity,
            VoteAverage = show.Vote_Average
        };
        showTitle.SetMediaData(new TvShowData(show.Number_Of_Seasons, show.Number_Of_Episodes));

        return new TitleWithCredits(showTitle, []);
    }

    private static List<TitleCredit> BuildCredits(Guid titleId, TmdbCreditsResponse credits)
    {
        var result = new List<TitleCredit>();
        var order = 0;

        foreach (var crew in credits.Crew.Where(c => c.Job == "Director").Take(3))
        {
            result.Add(new TitleCredit
            {
                TitleId = titleId,
                ExternalPersonId = $"tmdb-{crew.Id}",
                Name = crew.Name,
                Role = "director",
                DisplayOrder = order++
            });
        }

        foreach (var crew in credits.Crew.Where(c => c.Department == "Writing").Take(3))
        {
            result.Add(new TitleCredit
            {
                TitleId = titleId,
                ExternalPersonId = $"tmdb-{crew.Id}",
                Name = crew.Name,
                Role = "writer",
                DisplayOrder = order++
            });
        }

        foreach (var cast in credits.Cast.OrderBy(c => c.Order).Take(10))
        {
            result.Add(new TitleCredit
            {
                TitleId = titleId,
                ExternalPersonId = $"tmdb-{cast.Id}",
                Name = cast.Name,
                Role = "actor",
                DisplayOrder = order++
            });
        }

        return result;
    }

    internal static int? ParseYear(string? date) =>
        DateTime.TryParse(date, out var d) ? d.Year : null;

    internal static string? PosterUrl(string? posterPath) =>
        posterPath is not null ? $"https://image.tmdb.org/t/p/w342{posterPath}" : null;

    internal static string? BackdropUrl(string? backdropPath) =>
        backdropPath is not null ? $"https://image.tmdb.org/t/p/w1280{backdropPath}" : null;
}
