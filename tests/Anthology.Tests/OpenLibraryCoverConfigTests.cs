using Anthology.Modules.Catalog;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Anthology.Tests;

[CollectionDefinition(nameof(OpenLibraryCoverConfigTests), DisableParallelization = true)]
public class OpenLibraryCoverConfigCollection { }

[Collection(nameof(OpenLibraryCoverConfigTests))]
public class OpenLibraryCoverConfigTests
{
    [Fact]
    public async Task Configured_cover_base_url_is_used_for_search_results()
    {
        var provider = CreateProvider(
            new OpenLibraryOptions { CoverBaseUrl = "https://covers.example.test/b/id" }
        );

        var results = await provider.SearchAsync("fight club", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].PosterUrl.Should().Be("https://covers.example.test/b/id/8739161-M.jpg");
    }

    [Fact]
    public async Task Environment_variable_changes_the_cover_base_url()
    {
        const string variable = "OpenLibrary__CoverBaseUrl";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "https://covers.example.test/b/id");

        try
        {
            var provider = new OpenLibraryProvider(CreateApi());

            var results = await provider.SearchAsync("fight club", CancellationToken.None);

            results.Should().ContainSingle();
            results[0].PosterUrl.Should().Be("https://covers.example.test/b/id/8739161-M.jpg");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_or_blank_cover_base_url_keeps_the_default(string? coverBaseUrl)
    {
        var provider = CreateProvider(new OpenLibraryOptions { CoverBaseUrl = coverBaseUrl! });

        var results = await provider.SearchAsync("fight club", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].PosterUrl.Should().Be("https://covers.openlibrary.org/b/id/8739161-M.jpg");
    }

    [Fact]
    public async Task Configured_cover_base_url_is_used_for_details()
    {
        var provider = CreateProvider(
            new OpenLibraryOptions { CoverBaseUrl = "https://covers.example.test/b/id" }
        );

        var result = await provider.GetDetailsAsync("ol-OL45883W", CancellationToken.None);

        result!.Title.PosterPath.Should().Be("https://covers.example.test/b/id/8739161-M.jpg");
    }

    private static OpenLibraryProvider CreateProvider(OpenLibraryOptions options) =>
        new(CreateApi(), Options.Create(options));

    private static FakeOpenLibraryApi CreateApi() =>
        new()
        {
            Work = new OpenLibraryWork
            {
                Key = "/works/OL45883W",
                Title = "Fight Club",
                Covers = [8739161],
            },
            SearchResponse = new OpenLibrarySearchResponse([
                new OpenLibraryDoc
                {
                    Key = "/works/OL45883W",
                    Title = "Fight Club",
                    Cover_I = 8739161,
                },
            ]),
        };

    private sealed class FakeOpenLibraryApi : IOpenLibraryApi
    {
        public OpenLibrarySearchResponse SearchResponse { get; init; } = new([]);
        public OpenLibraryWork Work { get; init; } = new();
        public OpenLibraryTrendingResponse TrendingResponse { get; init; } = new();
        public OpenLibrarySubjectResponse SubjectResponse { get; init; } = new();

        public Task<OpenLibrarySearchResponse> SearchAsync(
            string query,
            int limit = 20,
            CancellationToken ct = default
        ) => Task.FromResult(SearchResponse);

        public Task<OpenLibraryWork> GetWorkAsync(string workId, CancellationToken ct = default) =>
            Task.FromResult(Work);

        public Task<OpenLibraryTrendingResponse> GetTrendingAsync(
            string period,
            int limit = 20,
            int page = 1,
            CancellationToken ct = default
        ) => Task.FromResult(TrendingResponse);

        public Task<OpenLibrarySubjectResponse> GetSubjectAsync(
            string subject,
            int limit = 20,
            int offset = 0,
            CancellationToken ct = default
        ) => Task.FromResult(SubjectResponse);
    }
}
