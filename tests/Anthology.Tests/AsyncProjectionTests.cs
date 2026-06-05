using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Anthology.Tests;

public sealed class TestProjection : IProjection
{
    public List<string> Applied { get; } = [];

    public Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var e in events)
        {
            if (e.Event is TestEvent te)
                Applied.Add(te.Value);
        }
        return Task.CompletedTask;
    }
}

public sealed class AsyncProjectionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private EventStoreDbContext CreateDb() => fixture.CreateEventStoreDbContext();

    private (EventStore Store, EventSerializer Serializer) CreateStoreAndSerializer(EventStoreDbContext db)
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event");
        var serializer = new EventSerializer(registry);
        return (new EventStore(db, registry, serializer), serializer);
    }

    private static EventMetadata TestMeta() =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task Catch_up_from_checkpoint_processes_only_new_events()
    {
        await using var db = CreateDb();
        var (store, serializer) = CreateStoreAndSerializer(db);
        var streamId = Guid.NewGuid();

        var state = new TestState("", 0);
        for (var i = 1; i <= 3; i++)
        {
            var events = new IDomainEvent[] { new TestEvent($"event-{i}") };
            state = events.Aggregate(state, (s, e) => e switch
            {
                TestEvent t => s with { Latest = t.Value, EventCount = s.EventCount + 1 },
                _ => s
            });
            await store.AppendAsync(streamId, "test", i - 1, events, state, TestMeta());
        }

        await using var readDb = CreateDb();
        var allEvents = await readDb.Events.AsNoTracking()
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.GlobalPosition)
            .ToListAsync();

        var checkpointPosition = allEvents[1].GlobalPosition;

        readDb.Checkpoints.Add(new CheckpointRow
        {
            ProjectionName = "TestProjection",
            Position = checkpointPosition
        });
        await readDb.SaveChangesAsync();

        var batch = await readDb.Events.AsNoTracking()
            .Where(e => e.GlobalPosition > checkpointPosition)
            .OrderBy(e => e.GlobalPosition)
            .ToListAsync();

        var projection = new TestProjection();
        var envelopes = batch.Select(row =>
        {
            var domainEvent = serializer.Deserialize(row.EventType, row.Payload);
            var metadata = serializer.DeserializeMetadata(row.Metadata);
            return new EventEnvelope(row.StreamId, row.Version, domainEvent, metadata);
        }).ToList();

        await projection.ApplyAsync(envelopes, CancellationToken.None);

        projection.Applied.Should().ContainSingle().Which.Should().Be("event-3");
    }

    [Fact]
    public async Task Checkpoint_row_created_with_default_position_zero()
    {
        await using var db = CreateDb();

        db.Checkpoints.Add(new CheckpointRow
        {
            ProjectionName = $"Projection_{Guid.NewGuid():N}",
            Position = 0
        });
        await db.SaveChangesAsync();

        var name = db.Checkpoints.Local.First().ProjectionName;
        await using var readDb = CreateDb();
        var checkpoint = await readDb.Checkpoints.FindAsync(name);
        checkpoint.Should().NotBeNull();
        checkpoint!.Position.Should().Be(0);
    }

    [Fact]
    public async Task Xid_guard_query_returns_committed_events()
    {
        await using var db = CreateDb();
        var (store, _) = CreateStoreAndSerializer(db);
        var streamId = Guid.NewGuid();

        var events = new IDomainEvent[] { new TestEvent("committed") };
        var state = new TestState("committed", 1);
        await store.AppendAsync(streamId, "test", 0, events, state, TestMeta());

        await using var readDb = CreateDb();
        var batch = await readDb.Events
            .FromSqlInterpolated($"""
                SELECT * FROM es.events
                WHERE "GlobalPosition" > {0}
                  AND "Xid" < pg_snapshot_xmin(pg_current_snapshot())
                ORDER BY "GlobalPosition"
                LIMIT 500
                """)
            .AsNoTracking()
            .ToListAsync();

        batch.Should().ContainSingle();
        batch[0].StreamId.Should().Be(streamId);
    }
}
