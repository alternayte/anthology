using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class SimilarityTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task GetSimilar_returns_titles_with_genre_overlap_when_no_embedding()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var source = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Arrival",
            Year = 2016,
            Genres = ["Science Fiction", "Drama"],
            Keywords = ["alien contact", "linguistics"],
            Popularity = 50.0
        };

        var similar = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Contact",
            Year = 1997,
            Genres = ["Science Fiction", "Drama"],
            Keywords = ["alien contact"],
            Popularity = 40.0
        };

        var unrelated = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "The Notebook",
            Year = 2004,
            Genres = ["Romance", "Drama"],
            Keywords = ["love story"],
            Popularity = 60.0
        };

        db.Titles.AddRange(source, similar, unrelated);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetSimilar.Handler(db);
        var results = await handler.Handle(source.TitleId, TestContext.Current.CancellationToken);

        results.Should().Contain(r => r.TitleId == similar.TitleId);

        var contactResult = results.FirstOrDefault(r => r.TitleId == similar.TitleId);
        var notebookResult = results.FirstOrDefault(r => r.TitleId == unrelated.TitleId);

        contactResult.Should().NotBeNull("Contact shares both genres with Arrival");

        if (notebookResult is not null)
        {
            var contactIndex = results.ToList().IndexOf(contactResult!);
            var notebookIndex = results.ToList().IndexOf(notebookResult);
            contactIndex.Should().BeLessThan(notebookIndex,
                "Contact has higher genre+keyword overlap than The Notebook");
        }
    }

    [Fact]
    public async Task GetSimilar_only_returns_same_media_type()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var sourceFilm = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Blade Runner 2049",
            Year = 2017,
            Genres = ["Science Fiction"],
            Keywords = ["replicant"],
            Popularity = 80.0
        };

        var otherFilm = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Ex Machina",
            Year = 2014,
            Genres = ["Science Fiction"],
            Keywords = ["artificial intelligence"],
            Popularity = 60.0
        };

        var book = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"sim-{Guid.NewGuid():N}",
            MediaType = MediaType.Book,
            Name = "Neuromancer",
            Year = 1984,
            Genres = ["Science Fiction"],
            Keywords = ["cyberpunk"],
            Popularity = 30.0
        };

        db.Titles.AddRange(sourceFilm, otherFilm, book);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetSimilar.Handler(db);
        var results = await handler.Handle(sourceFilm.TitleId, TestContext.Current.CancellationToken);

        results.Should().Contain(r => r.TitleId == otherFilm.TitleId);
        results.Should().NotContain(r => r.TitleId == book.TitleId,
            "books should not appear in similar results for a film");
        results.Should().OnlyContain(r => r.MediaType == MediaType.Film);
    }
}
