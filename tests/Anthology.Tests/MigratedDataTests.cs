using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;
using Anthology.Tests.Fixtures;
using Deedbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

/// <summary>
/// Fixtures/legacy-es.sql holds data that the pre-Deedbox event store wrote for one user: four films, two episodes of
/// one show, and two lists. One ItemWanted is stored as v1, two ratings under the old "rerated" name, and the old
/// global positions have a gap. These tests check what the migration and the rebuild made of it.
/// </summary>
public sealed class MigratedDataTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private static readonly Guid LegacyUser = Guid.Parse("01a0d269-9da4-7fed-89aa-75d3b33686bc");
    private static readonly Guid FilmOne = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid FilmTwo = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid FilmThree = Guid.Parse("11111111-0000-0000-0000-000000000003");
    private static readonly Guid FilmFour = Guid.Parse("11111111-0000-0000-0000-000000000004");
    private static readonly Guid Show = Guid.Parse("11111111-0000-0000-0000-000000000010");
    private static readonly Guid EpisodeOne = Guid.Parse("11111111-0000-0000-0000-000000000012");
    private static readonly Guid EpisodeTwo = Guid.Parse("11111111-0000-0000-0000-000000000013");
    private static readonly Guid FavouritesList = Guid.Parse("b64e0e3b-6796-47b7-8e6a-1dbdb0f7ad07");
    private static readonly Guid DeletedList = Guid.Parse("4805ac94-0c7a-4fcb-81d0-d2cf3d644d54");

    [Fact]
    public async Task The_library_is_rebuilt_from_the_migrated_events()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var items = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().LibraryItems.AsNoTracking()
            .Where(i => i.UserId == LegacyUser)
            .ToDictionaryAsync(i => i.TitleId, TestContext.Current.CancellationToken);

        items.Keys.Should().BeEquivalentTo([FilmOne, FilmTwo, FilmThree, FilmFour, EpisodeOne, EpisodeTwo, Show]);
        (items[FilmOne].Status, items[FilmOne].Rating, items[FilmOne].Title, items[FilmOne].PosterPath)
            .Should().Be((TrackedStatus.Finished, 9, "Legacy Film One", "/legacy1.jpg"));
        items[FilmTwo].Status.Should().Be(TrackedStatus.InProgress);
        items[FilmFour].Status.Should().Be(TrackedStatus.Abandoned);
        (items[FilmThree].Status, items[FilmThree].Title, items[FilmThree].MediaType)
            .Should().Be((TrackedStatus.WantToConsume, "Unknown", MediaType.Film), "the v1 event is upcast");
        (items[Show].Status, items[Show].PartsCompleted, items[Show].PartsTotal)
            .Should().Be((TrackedStatus.InProgress, 1, 2));
    }

    [Fact]
    public async Task The_diary_holds_one_entry_per_migrated_tracking_event()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var entries = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().DiaryEntries.AsNoTracking()
            .Where(e => e.UserId == LegacyUser && e.TitleId == FilmOne)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new { e.Status, e.Rating })
            .ToListAsync(TestContext.Current.CancellationToken);

        entries.Select(e => (e.Status, e.Rating)).Should().Equal(
            (TrackedStatus.WantToConsume, null), (TrackedStatus.InProgress, null), (TrackedStatus.Finished, null),
            (TrackedStatus.Rerated, 8), (TrackedStatus.Rerated, 9));
    }

    [Fact]
    public async Task Lists_are_rebuilt_with_their_order_renames_and_deletions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var lists = await db.Lists.AsNoTracking().Where(l => l.UserId == LegacyUser).ToDictionaryAsync(l => l.ListId, ct);
        var items = await db.ListItems.AsNoTracking().Where(i => i.ListId == FavouritesList)
            .OrderBy(i => i.Position).Select(i => i.TitleId).ToListAsync(ct);

        (lists[FavouritesList].Name, lists[FavouritesList].Description, lists[FavouritesList].Visibility, lists[FavouritesList].ItemCount)
            .Should().Be(("Legacy Favourites Renamed", null, ListVisibility.PublicByLink, 3));
        lists[DeletedList].IsDeleted.Should().BeTrue();
        items.Should().Equal(FilmOne, FilmFour, FilmTwo);
    }

    [Fact]
    public async Task Every_state_type_is_a_registered_stream()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var load = typeof(IEventStore).GetMethod(nameof(IEventStore.Load))!;
        var stateTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IState<>)))
            .ToList();

        stateTypes.Should().NotBeEmpty();
        foreach (var type in stateTypes)
        {
            var loading = (Task)load.MakeGenericMethod(type).Invoke(store, [$"convention-{Guid.NewGuid()}", TestContext.Current.CancellationToken])!;
            await loading.Invoking(t => t).Should().NotThrowAsync($"{type.Name} must be registered with Stream<T>(...)");
        }
    }
}

/// <summary>Writes to the migrated streams, in its own database so the read-model checks above see the migrated state.</summary>
public sealed class MigratedStreamTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private static readonly Guid LegacyUser = Guid.Parse("01a0d269-9da4-7fed-89aa-75d3b33686bc");
    private static readonly Guid FilmOne = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid FilmTwo = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid FavouritesList = Guid.Parse("b64e0e3b-6796-47b7-8e6a-1dbdb0f7ad07");

    [Fact]
    public async Task Migrated_streams_accept_new_commands_at_their_old_ids_and_versions()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = fixture.Factory.CreateClient();
        await client.PostAsJsonAsync("/api/identity/login", new { Email = "legacy@example.com", Password = "LegacyPassword123" }, ct);

        var again = await client.PostAsJsonAsync($"/api/tracking/items/{FilmOne}/want", new { }, ct);
        var rated = await client.PostAsJsonAsync($"/api/tracking/items/{FilmTwo}/rate", new { Rating = 7 }, ct);
        var reordered = await client.PutAsJsonAsync($"/api/tracking/lists/{FavouritesList}/items/{FilmTwo}/position",
            new { AfterTitleId = (Guid?)null }, ct);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict, "the item's migrated stream is found under the same ID");
        rated.StatusCode.Should().Be(HttpStatusCode.OK);
        reordered.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = fixture.Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var item = await store.Load<TrackedItemState>(TrackedItem.StreamIdFor(LegacyUser, FilmTwo), ct);
        (item.Version, item.State.Status, item.State.Rating?.Value).Should().Be((3, TrackedStatus.InProgress, 7));

        var entries = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().DiaryEntries.AsNoTracking()
            .CountAsync(e => e.UserId == LegacyUser && e.TitleId == FilmTwo && e.Status == TrackedStatus.Rerated, ct);
        entries.Should().Be(1, "the new rating is projected inline");
    }
}
