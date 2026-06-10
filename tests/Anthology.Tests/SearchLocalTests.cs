using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class SearchLocalTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task SearchLocal_returns_titles_matching_query_ordered_by_relevance()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var popular = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"local-search-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Interstellar",
            Year = 2014,
            Overview = "A team of explorers travel through a wormhole in space.",
            Popularity = 200.0,
            VoteAverage = 8.6
        };

        var obscure = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"local-search-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Interstellar Wars",
            Year = 2016,
            Overview = "Aliens attack earth and interstellar war begins.",
            Popularity = 1.0,
            VoteAverage = 2.0
        };

        db.Titles.AddRange(popular, obscure);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new SearchLocal.Handler(db);
        var results = await handler.Handle(
            new SearchLocal.Query("Interstellar", null, null),
            TestContext.Current.CancellationToken);

        results.Should().HaveCountGreaterThanOrEqualTo(2);

        var popularResult = results.First(r => r.TitleId == popular.TitleId);
        var obscureResult = results.First(r => r.TitleId == obscure.TitleId);

        var popularIndex = results.ToList().IndexOf(popularResult);
        var obscureIndex = results.ToList().IndexOf(obscureResult);
        popularIndex.Should().BeLessThan(obscureIndex,
            "the more popular title should rank higher");
    }

    [Fact]
    public async Task SearchLocal_filters_by_media_type()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var film = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"local-search-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Dune",
            Year = 2021,
            Overview = "A noble family becomes embroiled in a war for control of the most valuable asset."
        };

        var book = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"local-search-{Guid.NewGuid():N}",
            MediaType = MediaType.Book,
            Name = "Dune",
            Year = 1965,
            Overview = "Set on the desert planet Arrakis, Dune is the story of Paul Atreides."
        };

        db.Titles.AddRange(film, book);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new SearchLocal.Handler(db);
        var results = await handler.Handle(
            new SearchLocal.Query("Dune", MediaType.Book, null),
            TestContext.Current.CancellationToken);

        results.Should().Contain(r => r.TitleId == book.TitleId);
        results.Should().NotContain(r => r.TitleId == film.TitleId);
    }
}
