using Anthology.Modules.Catalog;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public class CatalogProviderTests
{
    [Fact]
    public void TmdbProvider_maps_movie_to_CatalogSearchResult()
    {
        var movie = new TmdbMovie(550, "Fight Club", "A first-generation member...", "1999-10-15", "/poster.jpg");
        var result = TmdbProvider.MapMovieResult(movie);

        result.ExternalId.Should().Be("tmdb-550");
        result.MediaType.Should().Be(MediaType.Film);
        result.Name.Should().Be("Fight Club");
        result.Year.Should().Be(1999);
        result.PosterUrl.Should().Be("https://image.tmdb.org/t/p/w342/poster.jpg");
    }

    [Fact]
    public void TmdbProvider_maps_tv_show_to_CatalogSearchResult()
    {
        var show = new TmdbTvShow(1396, "Breaking Bad", "A chemistry teacher...", "2008-01-20", "/bb.jpg", 5, 62);
        var result = TmdbProvider.MapTvResult(show);

        result.ExternalId.Should().Be("tmdb-tv-1396");
        result.MediaType.Should().Be(MediaType.TvShow);
        result.Name.Should().Be("Breaking Bad");
        result.Year.Should().Be(2008);
    }

    [Theory]
    [InlineData("tmdb-550", true)]
    [InlineData("tmdb-tv-1396", true)]
    [InlineData("ol-OL45883W", false)]
    [InlineData("igdb-1942", false)]
    public void TmdbProvider_OwnsExternalId(string externalId, bool expected)
    {
        var provider = new TmdbProvider(null!);
        provider.OwnsExternalId(externalId).Should().Be(expected);
    }

    [Fact]
    public void OpenLibraryProvider_maps_doc_to_CatalogSearchResult()
    {
        var doc = new OpenLibraryDoc
        {
            Key = "/works/OL45883W",
            Title = "Fight Club",
            First_Publish_Year = 1996,
            Author_Name = ["Chuck Palahniuk"],
            Cover_I = 8739161,
            Number_Of_Pages_Median = 218
        };
        var result = OpenLibraryProvider.MapSearchResult(doc);

        result.ExternalId.Should().Be("ol-OL45883W");
        result.MediaType.Should().Be(MediaType.Book);
        result.Name.Should().Be("Fight Club");
        result.Year.Should().Be(1996);
        result.PosterUrl.Should().Be("https://covers.openlibrary.org/b/id/8739161-M.jpg");
    }

    [Theory]
    [InlineData("ol-OL45883W", true)]
    [InlineData("tmdb-550", false)]
    public void OpenLibraryProvider_OwnsExternalId(string externalId, bool expected)
    {
        var provider = new OpenLibraryProvider(null!);
        provider.OwnsExternalId(externalId).Should().Be(expected);
    }

    [Fact]
    public void IgdbProvider_maps_game_to_CatalogSearchResult()
    {
        var game = new IgdbGame
        {
            Id = 1942,
            Name = "The Witcher 3",
            First_Release_Date = 1431993600,
            Summary = "An RPG...",
            Cover = new IgdbCover { Image_Id = "co1234" },
            Involved_Companies = [new IgdbInvolvedCompany { Developer = true, Company = new IgdbCompany { Name = "CD Projekt Red" } }],
            Platforms = [new IgdbPlatform { Name = "PC" }, new IgdbPlatform { Name = "PlayStation 4" }]
        };
        var result = IgdbProvider.MapSearchResult(game);

        result.ExternalId.Should().Be("igdb-1942");
        result.MediaType.Should().Be(MediaType.Game);
        result.Name.Should().Be("The Witcher 3");
        result.Year.Should().Be(2015);
        result.PosterUrl.Should().Be("https://images.igdb.com/igdb/image/upload/t_cover_big/co1234.jpg");
    }

    [Theory]
    [InlineData("igdb-1942", true)]
    [InlineData("tmdb-550", false)]
    public void IgdbProvider_OwnsExternalId(string externalId, bool expected)
    {
        var provider = new IgdbProvider(null);
        provider.OwnsExternalId(externalId).Should().Be(expected);
    }

    [Fact]
    public void MusicBrainzProvider_maps_release_group_to_CatalogSearchResult()
    {
        var rg = new MusicBrainzReleaseGroup
        {
            Id = "67a63246-0de4-3c8b-9f44-7146b2890e94",
            Title = "OK Computer",
            Primary_Type = "Album",
            First_Release_Date = "1997-05-21",
            Artist_Credit = [new MusicBrainzArtistCredit { Name = "Radiohead" }]
        };
        var result = MusicBrainzProvider.MapSearchResult(rg);

        result.ExternalId.Should().Be("mb-67a63246-0de4-3c8b-9f44-7146b2890e94");
        result.MediaType.Should().Be(MediaType.Music);
        result.Name.Should().Be("OK Computer");
        result.Year.Should().Be(1997);
        result.PosterUrl.Should().Be("https://coverartarchive.org/release-group/67a63246-0de4-3c8b-9f44-7146b2890e94/front-250");
        result.Overview.Should().Be("Radiohead — Album");
    }

    [Theory]
    [InlineData("mb-67a63246-0de4-3c8b-9f44-7146b2890e94", true)]
    [InlineData("tmdb-550", false)]
    public void MusicBrainzProvider_OwnsExternalId(string externalId, bool expected)
    {
        var provider = new MusicBrainzProvider(null);
        provider.OwnsExternalId(externalId).Should().Be(expected);
    }

    [Fact]
    public async Task SearchTitles_fans_out_to_all_providers_when_no_filter()
    {
        var providerA = new FakeCatalogProvider(MediaType.Film, "tmdb-",
            [new CatalogSearchResult("tmdb-1", MediaType.Film, "Film A", 2020, null, null)]);
        var providerB = new FakeCatalogProvider(MediaType.Book, "ol-",
            [new CatalogSearchResult("ol-1", MediaType.Book, "Book A", 2020, null, null)]);

        var handler = new SearchTitles.Handler([providerA, providerB]);
        var results = await handler.Handle(new SearchTitles.Query("test", null), CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.ExternalId == "tmdb-1");
        results.Should().Contain(r => r.ExternalId == "ol-1");
    }

    [Fact]
    public async Task SearchTitles_filters_to_matching_provider()
    {
        var providerA = new FakeCatalogProvider(MediaType.Film, "tmdb-",
            [new CatalogSearchResult("tmdb-1", MediaType.Film, "Film A", 2020, null, null)]);
        var providerB = new FakeCatalogProvider(MediaType.Book, "ol-",
            [new CatalogSearchResult("ol-1", MediaType.Book, "Book A", 2020, null, null)]);

        var handler = new SearchTitles.Handler([providerA, providerB]);
        var results = await handler.Handle(new SearchTitles.Query("test", MediaType.Book), CancellationToken.None);

        results.Should().HaveCount(1);
        results.Should().Contain(r => r.ExternalId == "ol-1");
    }

    [Fact]
    public async Task SearchTitles_returns_partial_results_when_provider_fails()
    {
        var good = new FakeCatalogProvider(MediaType.Film, "tmdb-",
            [new CatalogSearchResult("tmdb-1", MediaType.Film, "Film A", 2020, null, null)]);
        var bad = new FailingCatalogProvider(MediaType.Book);

        var handler = new SearchTitles.Handler([good, bad]);
        var results = await handler.Handle(new SearchTitles.Query("test", null), CancellationToken.None);

        results.Should().HaveCount(1);
        results.Should().Contain(r => r.ExternalId == "tmdb-1");
    }

    [Fact]
    public async Task TmdbProvider_GetDetailsAsync_maps_backdrop_path()
    {
        var api = new StubTmdbApi
        {
            MovieDetail = new TmdbMovieDetail(
                550, "Fight Club", "A first-generation member...", "1999-10-15", "/poster.jpg",
                10.0, 8.4, [], new TmdbKeywordsResponse([]), new TmdbCreditsResponse([], []),
                "/backdrop.jpg")
        };

        var provider = new TmdbProvider(api);
        var result = await provider.GetDetailsAsync("tmdb-550", CancellationToken.None);

        result!.Title.BackdropPath.Should().Be("https://image.tmdb.org/t/p/w1280/backdrop.jpg");
        result.Title.PosterPath.Should().Be("https://image.tmdb.org/t/p/w342/poster.jpg");
    }

    private sealed class StubTmdbApi : ITmdbApi
    {
        public TmdbMovieDetail MovieDetail { get; init; } = default!;

        public Task<TmdbMovieDetail> GetMovieDetailAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(MovieDetail);

        public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(string query, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbMovie> GetMovieAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbTvShow>> SearchTvAsync(string query, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbTvShow> GetTvShowAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbTvShowDetail> GetTvShowDetailAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbSeason> GetSeasonAsync(int id, int seasonNumber, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbMovie>> GetPopularMoviesAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbMovie>> GetTopRatedMoviesAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbMovie>> GetTrendingMoviesAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbTvShow>> GetPopularTvAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbTvShow>> GetTopRatedTvAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TmdbPagedResult<TmdbTvShow>> GetTrendingTvAsync(int page, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCatalogProvider(MediaType type, string prefix, IReadOnlyList<CatalogSearchResult> results) : ICatalogProvider
    {
        public IReadOnlySet<MediaType> SupportedTypes { get; } = new HashSet<MediaType> { type }.AsReadOnly();
        public bool OwnsExternalId(string externalId) => externalId.StartsWith(prefix);
        public Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct) =>
            Task.FromResult(results);
        public Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct) =>
            Task.FromResult<TitleWithCredits?>(null);
    }

    private sealed class FailingCatalogProvider(MediaType type) : ICatalogProvider
    {
        public IReadOnlySet<MediaType> SupportedTypes { get; } = new HashSet<MediaType> { type }.AsReadOnly();
        public bool OwnsExternalId(string externalId) => false;
        public Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct) =>
            throw new HttpRequestException("Simulated failure");
        public Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct) =>
            throw new HttpRequestException("Simulated failure");
    }
}
