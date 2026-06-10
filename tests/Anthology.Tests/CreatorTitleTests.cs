using Anthology.Modules.Catalog;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class CreatorTitleTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task GetCreatorTitles_returns_titles_sharing_director()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var inception = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Inception",
            Year = 2010,
            Popularity = 80.0
        };

        var darkKnight = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "The Dark Knight",
            Year = 2008,
            Popularity = 90.0
        };

        var godfather = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "The Godfather",
            Year = 1972,
            Popularity = 85.0
        };

        db.Titles.AddRange(inception, darkKnight, godfather);

        var nolanId = "person-nolan";
        var coppolaId = "person-coppola";

        db.TitleCredits.AddRange(
            new TitleCredit { TitleId = inception.TitleId, ExternalPersonId = nolanId, Name = "Christopher Nolan", Role = "director", DisplayOrder = 0 },
            new TitleCredit { TitleId = darkKnight.TitleId, ExternalPersonId = nolanId, Name = "Christopher Nolan", Role = "director", DisplayOrder = 0 },
            new TitleCredit { TitleId = godfather.TitleId, ExternalPersonId = coppolaId, Name = "Francis Ford Coppola", Role = "director", DisplayOrder = 0 }
        );

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetCreatorTitles.Handler(db);
        var results = await handler.Handle(inception.TitleId, TestContext.Current.CancellationToken);

        results.Should().ContainSingle(r => r.TitleId == darkKnight.TitleId);
        results.Should().NotContain(r => r.TitleId == godfather.TitleId);

        var match = results.First(r => r.TitleId == darkKnight.TitleId);
        match.SharedPerson.Should().Be("Christopher Nolan");
        match.SharedRole.Should().Be("director");
    }

    [Fact]
    public async Task GetCreatorTitles_prioritizes_directors_over_actors()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var memento = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Memento",
            Year = 2000,
            Popularity = 60.0
        };

        var tenet = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "Tenet",
            Year = 2020,
            Popularity = 70.0
        };

        var matrix = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"ct-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = "The Matrix",
            Year = 1999,
            Popularity = 95.0
        };

        db.Titles.AddRange(memento, tenet, matrix);

        var nolanId = $"person-nolan-{Guid.NewGuid():N}";
        var actorId = $"person-actor-{Guid.NewGuid():N}";

        db.TitleCredits.AddRange(
            new TitleCredit { TitleId = memento.TitleId, ExternalPersonId = nolanId, Name = "Christopher Nolan", Role = "director", DisplayOrder = 0 },
            new TitleCredit { TitleId = tenet.TitleId, ExternalPersonId = nolanId, Name = "Christopher Nolan", Role = "director", DisplayOrder = 0 },
            new TitleCredit { TitleId = memento.TitleId, ExternalPersonId = actorId, Name = "Carrie-Anne Moss", Role = "actor", DisplayOrder = 1 },
            new TitleCredit { TitleId = matrix.TitleId, ExternalPersonId = actorId, Name = "Carrie-Anne Moss", Role = "actor", DisplayOrder = 1 }
        );

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetCreatorTitles.Handler(db);
        var results = await handler.Handle(memento.TitleId, TestContext.Current.CancellationToken);

        results.Should().HaveCountGreaterThanOrEqualTo(2);

        var tenetResult = results.First(r => r.TitleId == tenet.TitleId);
        var matrixResult = results.First(r => r.TitleId == matrix.TitleId);

        var tenetIndex = results.ToList().IndexOf(tenetResult);
        var matrixIndex = results.ToList().IndexOf(matrixResult);

        tenetIndex.Should().BeLessThan(matrixIndex,
            "Tenet (director match) should come before The Matrix (actor match)");
    }
}
