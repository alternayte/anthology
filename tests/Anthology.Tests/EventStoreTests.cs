using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public sealed record TestEvent(string Value) : IDomainEvent;
public sealed record AnotherTestEvent(int Count) : IDomainEvent;

public sealed class EventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private EventStore CreateStore(EventStoreDbContext db)
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event");
        registry.Map<AnotherTestEvent>("test.another");
        var serializer = new EventSerializer(registry);
        return new EventStore(db, registry, serializer);
    }

    private static EventMetadata TestMeta() =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task Append_and_rehydrate_round_trips()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        await store.AppendAsync(streamId, 0, [new TestEvent("hello")], TestMeta(), TestContext.Current.CancellationToken);

        await using var readDb = fixture.CreateEventStoreDbContext();
        var readStore = CreateStore(readDb);
        var events = new List<IDomainEvent>();
        await readStore.RehydrateAsync(streamId, 0, (_, e) => { events.Add(e); return 0; }, TestContext.Current.CancellationToken);

        events.Should().ContainSingle().Which.Should().BeOfType<TestEvent>()
            .Which.Value.Should().Be("hello");
    }

    [Fact]
    public async Task Append_multiple_events_increments_version()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        await store.AppendAsync(streamId, 0,
            [new TestEvent("one"), new AnotherTestEvent(2)], TestMeta(), TestContext.Current.CancellationToken);

        await using var readDb = fixture.CreateEventStoreDbContext();
        var readStore = CreateStore(readDb);
        var (_, version) = await readStore.RehydrateWithVersionAsync(
            streamId, 0, (count, _) => count + 1, TestContext.Current.CancellationToken);

        version.Should().Be(2);
    }

    [Fact]
    public async Task Append_with_wrong_version_throws_ConcurrencyConflict()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        await store.AppendAsync(streamId, 0, [new TestEvent("first")], TestMeta(), TestContext.Current.CancellationToken);

        await using var db2 = fixture.CreateEventStoreDbContext();
        var store2 = CreateStore(db2);

        var act = () => store2.AppendAsync(streamId, 0, [new TestEvent("conflict")], TestMeta(), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConcurrencyConflict>();
    }

    [Fact]
    public async Task Rehydrate_empty_stream_returns_initial_state()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);

        var (state, version) = await store.RehydrateWithVersionAsync(
            Guid.NewGuid(), "initial", (s, _) => "changed", TestContext.Current.CancellationToken);

        state.Should().Be("initial");
        version.Should().Be(0);
    }

    [Fact]
    public async Task Rehydrate_applies_upcaster_to_old_version_event()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json => json["value"] = "upcasted-from-v1")
        ]);
        var serializer = new EventSerializer(registry);

        await using var db = fixture.CreateEventStoreDbContext();

        var streamId = Guid.NewGuid();
        var v1Payload = """{"value":"original"}""";
        db.Events.Add(new EventRow
        {
            StreamId = streamId,
            Version = 1,
            EventType = "test.event.v1",
            Payload = v1Payload,
            Metadata = serializer.SerializeMetadata(TestMeta()),
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readDb = fixture.CreateEventStoreDbContext();
        var store = new EventStore(readDb, registry, serializer);
        var events = new List<IDomainEvent>();
        await store.RehydrateAsync(streamId, 0, (_, e) => { events.Add(e); return 0; }, TestContext.Current.CancellationToken);

        events.Should().ContainSingle().Which.Should().BeOfType<TestEvent>()
            .Which.Value.Should().Be("upcasted-from-v1");
    }
}
