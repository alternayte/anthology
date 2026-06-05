using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Anthology.Tests;

public sealed record RebuildTestEvent(string Value) : IDomainEvent;

public sealed record RebuildTestState(string Latest, int EventCount) : IAggregateState<RebuildTestState>
{
    public static RebuildTestState Initial => new("", 0);
    public static string StreamType => "rebuild_test";
}

public sealed class StreamRebuilderTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static RebuildTestState Evolve(RebuildTestState s, IDomainEvent e) => e switch
    {
        RebuildTestEvent t => s with { Latest = t.Value, EventCount = s.EventCount + 1 },
        _ => s
    };

    private static EventMetadata TestMeta() =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    private EventRegistry CreateEventRegistry()
    {
        var eventRegistry = new EventRegistry();
        eventRegistry.Map<RebuildTestEvent>("rebuild.test.event");
        return eventRegistry;
    }

    private (EventStore Store, StreamRebuilder Rebuilder, EventSerializer Serializer) CreateServices(
        EventStoreDbContext db)
    {
        var eventRegistry = CreateEventRegistry();
        var serializer = new EventSerializer(eventRegistry);
        var evolverRegistry = new StreamEvolverRegistry();
        evolverRegistry.Register<RebuildTestState>(serializer, Evolve);
        var store = new EventStore(db, eventRegistry, serializer);
        var rebuilder = new StreamRebuilder(db, evolverRegistry);
        return (store, rebuilder, serializer);
    }

    [Fact]
    public async Task RebuildStreamAsync_recomputes_state_from_events()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateEventStoreDbContext();
        var (store, _, _) = CreateServices(db);
        var streamId = Guid.NewGuid();

        var events1 = new IDomainEvent[] { new RebuildTestEvent("first") };
        var state1 = events1.Aggregate(RebuildTestState.Initial, Evolve);
        await store.AppendAsync(streamId, "rebuild_test", 0, events1, state1, TestMeta(), ct);

        await using var db2 = fixture.CreateEventStoreDbContext();
        var (store2, _, _) = CreateServices(db2);
        var events2 = new IDomainEvent[] { new RebuildTestEvent("second") };
        var state2 = events2.Aggregate(state1, Evolve);
        await store2.AppendAsync(streamId, "rebuild_test", 1, events2, state2, TestMeta(), ct);

        // Corrupt state to simulate stale Evolve
        await using var corruptDb = fixture.CreateEventStoreDbContext();
        var stream = await corruptDb.Streams.FirstAsync(s => s.StreamId == streamId, ct);
        stream.State = """{"latest":"CORRUPTED","eventCount":999}""";
        await corruptDb.SaveChangesAsync(ct);

        // Rebuild
        await using var rebuildDb = fixture.CreateEventStoreDbContext();
        var (_, rebuilder, serializer) = CreateServices(rebuildDb);
        var result = await rebuilder.RebuildStreamAsync(streamId, ct);

        result.IsError.Should().BeFalse();
        result.Value.EventsReplayed.Should().Be(2);

        // Verify state was recomputed
        await using var verifyDb = fixture.CreateEventStoreDbContext();
        var rebuilt = await verifyDb.Streams.AsNoTracking()
            .FirstAsync(s => s.StreamId == streamId, ct);
        var rebuiltState = serializer.DeserializeState<RebuildTestState>(rebuilt.State);
        rebuiltState.Latest.Should().Be("second");
        rebuiltState.EventCount.Should().Be(2);
    }

    [Fact]
    public async Task RebuildStreamAsync_returns_not_found_for_unknown_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateEventStoreDbContext();
        var (_, rebuilder, _) = CreateServices(db);

        var result = await rebuilder.RebuildStreamAsync(Guid.NewGuid(), ct);

        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task CreateJobAsync_inserts_pending_job_row()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a stream so Total > 0
        await using var seedDb = fixture.CreateEventStoreDbContext();
        var (store, _, _) = CreateServices(seedDb);
        var streamId = Guid.NewGuid();
        var events = new IDomainEvent[] { new RebuildTestEvent("hello") };
        var state = events.Aggregate(RebuildTestState.Initial, Evolve);
        await store.AppendAsync(streamId, "rebuild_test", 0, events, state, TestMeta(), ct);

        // Create job
        await using var db = fixture.CreateEventStoreDbContext();
        var (_, rebuilder, _) = CreateServices(db);
        var result = await rebuilder.CreateJobAsync("rebuild_test", ct);

        result.IsError.Should().BeFalse();
        var jobId = result.Value;

        // Verify job row
        await using var verifyDb = fixture.CreateEventStoreDbContext();
        var job = await verifyDb.RebuildJobs.AsNoTracking()
            .FirstAsync(j => j.Id == jobId, ct);
        job.StreamType.Should().Be("rebuild_test");
        job.Status.Should().Be("pending");
        job.Total.Should().BeGreaterThanOrEqualTo(1);
        job.Processed.Should().Be(0);
        job.Failed.Should().Be(0);
    }

    [Fact]
    public async Task CreateJobAsync_returns_error_for_unregistered_stream_type()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateEventStoreDbContext();
        var (_, rebuilder, _) = CreateServices(db);

        var result = await rebuilder.CreateJobAsync("nonexistent_type", ct);

        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Unprocessable);
    }
}
