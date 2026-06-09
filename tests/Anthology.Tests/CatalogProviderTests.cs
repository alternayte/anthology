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
}
