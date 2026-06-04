using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Modules.Tracking;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Anthology.Tests;

public sealed record TestEvent(string Value) : IDomainEvent;
public sealed record AnotherTestEvent(int Count) : IDomainEvent;

public sealed record TestState(string Latest, int EventCount)
{
    public static readonly TestState Initial = new("", 0);
}

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

    private static TestState Evolve(TestState s, IDomainEvent e) => e switch
    {
        TestEvent t => s with { Latest = t.Value, EventCount = s.EventCount + 1 },
        AnotherTestEvent a => s with { Latest = $"count:{a.Count}", EventCount = s.EventCount + 1 },
        _ => s
    };

    [Fact]
    public async Task Append_creates_stream_row_with_correct_state()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();
        var events = new IDomainEvent[] { new TestEvent("hello") };
        var newState = events.Aggregate(TestState.Initial, Evolve);

        await store.AppendAsync(streamId, "test_aggregate", 0, events, newState, TestMeta());

        await using var readDb = fixture.CreateEventStoreDbContext();
        var stream = await readDb.Streams.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StreamId == streamId);
        stream.Should().NotBeNull();
        stream!.Version.Should().Be(1);
        stream.StreamType.Should().Be("test_aggregate");
    }

    [Fact]
    public async Task Append_updates_stream_row_on_subsequent_writes()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        var events1 = new IDomainEvent[] { new TestEvent("first") };
        var state1 = events1.Aggregate(TestState.Initial, Evolve);
        await store.AppendAsync(streamId, "test_aggregate", 0, events1, state1, TestMeta());

        await using var db2 = fixture.CreateEventStoreDbContext();
        var store2 = CreateStore(db2);
        var events2 = new IDomainEvent[] { new AnotherTestEvent(42) };
        var state2 = events2.Aggregate(state1, Evolve);
        await store2.AppendAsync(streamId, "test_aggregate", 1, events2, state2, TestMeta());

        await using var readDb = fixture.CreateEventStoreDbContext();
        var stream = await readDb.Streams.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StreamId == streamId);
        stream!.Version.Should().Be(2);
    }

    [Fact]
    public async Task Append_with_wrong_version_throws_ConcurrencyConflict()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        var events1 = new IDomainEvent[] { new TestEvent("first") };
        var state1 = events1.Aggregate(TestState.Initial, Evolve);
        await store.AppendAsync(streamId, "test_aggregate", 0, events1, state1, TestMeta());

        await using var db2 = fixture.CreateEventStoreDbContext();
        var store2 = CreateStore(db2);
        var events2 = new IDomainEvent[] { new TestEvent("conflict") };
        var state2 = events2.Aggregate(TestState.Initial, Evolve);

        var act = () => store2.AppendAsync(streamId, "test_aggregate", 0, events2, state2, TestMeta());
        await act.Should().ThrowAsync<ConcurrencyConflict>();
    }

    [Fact]
    public async Task Load_returns_inline_state()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);
        var streamId = Guid.NewGuid();

        var events = new IDomainEvent[] { new TestEvent("hello") };
        var newState = events.Aggregate(TestState.Initial, Evolve);
        await store.AppendAsync(streamId, "test_aggregate", 0, events, newState, TestMeta());

        await using var readDb = fixture.CreateEventStoreDbContext();
        var readStore = CreateStore(readDb);
        var (state, version) = await readStore.LoadAsync<TestState>(streamId);

        state.Latest.Should().Be("hello");
        state.EventCount.Should().Be(1);
        version.Should().Be(1);
    }

    [Fact]
    public async Task Load_unknown_stream_returns_default_and_version_zero()
    {
        await using var db = fixture.CreateEventStoreDbContext();
        var store = CreateStore(db);

        var (state, version) = await store.LoadAsync<TestState>(Guid.NewGuid());

        state.Should().Be(default(TestState));
        version.Should().Be(0);
    }

    [Fact]
    public void Serializer_round_trips_aggregate_state()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event");
        var serializer = new EventSerializer(registry);

        var state = new TrackedItemState(TrackedStatus.Finished, new Rating(8), Guid.NewGuid(), 3);
        var json = serializer.SerializeState(state);
        var deserialized = serializer.DeserializeState<TrackedItemState>(json);

        deserialized.Should().Be(state);
    }
}
