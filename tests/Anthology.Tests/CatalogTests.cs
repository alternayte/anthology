using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class CatalogTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task GetTitle_returns_TvShowDetailDto_with_nested_seasons_and_episodes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Seed show
        var show = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tv-test-{Guid.NewGuid():N}",
            MediaType = MediaType.TvShow,
            Name = "Breaking Bad",
            Year = 2008,
            PosterPath = "/poster.jpg",
            Overview = "A chemistry teacher turns to crime."
        };
        show.SetMediaData(new TvShowData(5, 62));
        db.Titles.Add(show);

        // Seed season
        var season = new Title
        {
            TitleId = Guid.NewGuid(),
            ParentTitleId = show.TitleId,
            ExternalId = $"tv-test-{show.TitleId}-s1",
            MediaType = MediaType.Season,
            Name = "Season 1",
            Year = 2008,
            SortOrder = 1
        };
        season.SetMediaData(new SeasonData(1, 7, "2008-01-20"));
        db.Titles.Add(season);

        // Seed two episodes
        var ep1 = new Title
        {
            TitleId = Guid.NewGuid(),
            ParentTitleId = season.TitleId,
            ExternalId = $"tv-test-{show.TitleId}-s1e1",
            MediaType = MediaType.Episode,
            Name = "Pilot",
            SortOrder = 1
        };
        ep1.SetMediaData(new EpisodeData(1, 1, "2008-01-20", "/still1.jpg"));
        db.Titles.Add(ep1);

        var ep2 = new Title
        {
            TitleId = Guid.NewGuid(),
            ParentTitleId = season.TitleId,
            ExternalId = $"tv-test-{show.TitleId}-s1e2",
            MediaType = MediaType.Episode,
            Name = "Cat's in the Bag",
            SortOrder = 2
        };
        ep2.SetMediaData(new EpisodeData(1, 2, "2008-01-27", "/still2.jpg"));
        db.Titles.Add(ep2);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var handler = new GetTitle.Handler(db);
        var result = await handler.Handle(show.TitleId, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeOfType<GetTitle.TvShowDetailDto>();

        var dto = (GetTitle.TvShowDetailDto)result.Value;
        dto.TitleId.Should().Be(show.TitleId);
        dto.Name.Should().Be("Breaking Bad");
        dto.Year.Should().Be(2008);
        dto.ShowData.NumberOfSeasons.Should().Be(5);
        dto.ShowData.NumberOfEpisodes.Should().Be(62);

        dto.Seasons.Should().HaveCount(1);
        var seasonDto = dto.Seasons[0];
        seasonDto.TitleId.Should().Be(season.TitleId);
        seasonDto.SeasonNumber.Should().Be(1);
        seasonDto.Episodes.Should().HaveCount(2);

        var ep1Dto = seasonDto.Episodes[0];
        ep1Dto.EpisodeNumber.Should().Be(1);
        ep1Dto.Name.Should().Be("Pilot");
        ep1Dto.AirDate.Should().Be("2008-01-20");
        ep1Dto.StillPath.Should().Be("/still1.jpg");

        var ep2Dto = seasonDto.Episodes[1];
        ep2Dto.EpisodeNumber.Should().Be(2);
        ep2Dto.Name.Should().Be("Cat's in the Bag");
    }

    [Fact]
    public async Task GetTitle_returns_flat_TitleDetailDto_for_film()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var film = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"film-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Inception",
            Year = 2010,
            Overview = "Dreams within dreams."
        };
        db.Titles.Add(film);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetTitle.Handler(db);
        var result = await handler.Handle(film.TitleId, TestContext.Current.CancellationToken);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeOfType<GetTitle.TvShowDetailDto>();
        result.Value.MediaType.Should().Be(MediaType.Film);
        result.Value.Name.Should().Be("Inception");
    }

    [Fact]
    public async Task Title_persists_and_retrieves_BookData()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var book = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"book-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Book,
            Name = "The Name of the Wind",
            Year = 2007
        };
        book.SetMediaData(new BookData("Patrick Rothfuss", 662, "978-0756404741"));
        db.Titles.Add(book);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await db.Titles.FindAsync([book.TitleId], TestContext.Current.CancellationToken);
        saved.Should().NotBeNull();
        saved!.MediaType.Should().Be(MediaType.Book);

        var data = saved.GetMediaData<BookData>();
        data.Should().NotBeNull();
        data!.Author.Should().Be("Patrick Rothfuss");
        data.PageCount.Should().Be(662);
        data.Isbn.Should().Be("978-0756404741");
    }

    [Fact]
    public async Task Title_persists_and_retrieves_GameData()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var game = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"game-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Game,
            Name = "Elden Ring",
            Year = 2022
        };
        game.SetMediaData(new GameData("FromSoftware", "Bandai Namco", ["PC", "PS5", "Xbox Series X"]));
        db.Titles.Add(game);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await db.Titles.FindAsync([game.TitleId], TestContext.Current.CancellationToken);
        saved.Should().NotBeNull();
        saved!.MediaType.Should().Be(MediaType.Game);

        var data = saved.GetMediaData<GameData>();
        data.Should().NotBeNull();
        data!.Developer.Should().Be("FromSoftware");
        data.Publisher.Should().Be("Bandai Namco");
        data.Platforms.Should().BeEquivalentTo(["PC", "PS5", "Xbox Series X"]);
    }

    [Fact]
    public async Task Title_persists_and_retrieves_MusicData()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var album = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"music-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Music,
            Name = "OK Computer",
            Year = 1997
        };
        album.SetMediaData(new MusicData("Radiohead", "Parlophone", 12, "album"));
        db.Titles.Add(album);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await db.Titles.FindAsync([album.TitleId], TestContext.Current.CancellationToken);
        saved.Should().NotBeNull();
        saved!.MediaType.Should().Be(MediaType.Music);

        var data = saved.GetMediaData<MusicData>();
        data.Should().NotBeNull();
        data!.Artist.Should().Be("Radiohead");
        data.Label.Should().Be("Parlophone");
        data.TrackCount.Should().Be(12);
        data.ReleaseType.Should().Be("album");
    }

    [Fact]
    public async Task Title_persists_genres_keywords_and_popularity()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var film = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"enrich-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Interstellar",
            Year = 2014,
            Overview = "A team of explorers travel through a wormhole in space.",
            Genres = ["Science Fiction", "Drama", "Adventure"],
            Keywords = ["space travel", "wormhole", "time dilation"],
            Popularity = 82.5,
            VoteAverage = 8.6
        };
        db.Titles.Add(film);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await db.Titles.FindAsync([film.TitleId], TestContext.Current.CancellationToken);
        saved.Should().NotBeNull();
        saved!.Genres.Should().BeEquivalentTo(["Science Fiction", "Drama", "Adventure"]);
        saved.Keywords.Should().BeEquivalentTo(["space travel", "wormhole", "time dilation"]);
        saved.Popularity.Should().Be(82.5);
        saved.VoteAverage.Should().Be(8.6);
        saved.Embedding.Should().BeNull();
    }

    [Fact]
    public async Task TitleCredit_persists_and_queries_by_person()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var titleId = Guid.NewGuid();
        var film = new Title
        {
            TitleId = titleId,
            ExternalId = $"credit-test-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Inception",
            Year = 2010
        };
        db.Titles.Add(film);

        db.TitleCredits.AddRange(
            new TitleCredit { TitleId = titleId, ExternalPersonId = "tmdb-525", Name = "Christopher Nolan", Role = "director", DisplayOrder = 0 },
            new TitleCredit { TitleId = titleId, ExternalPersonId = "tmdb-6193", Name = "Leonardo DiCaprio", Role = "actor", DisplayOrder = 1 }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var credits = await db.TitleCredits.AsNoTracking()
            .Where(c => c.TitleId == titleId)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(TestContext.Current.CancellationToken);

        credits.Should().HaveCount(2);
        credits[0].Name.Should().Be("Christopher Nolan");
        credits[0].Role.Should().Be("director");
        credits[1].Name.Should().Be("Leonardo DiCaprio");
        credits[1].Role.Should().Be("actor");
    }
}
