using Microsoft.EntityFrameworkCore;

namespace Anthology.Kernel.EventStore;

public sealed class EventStore(EventStoreDbContext db, EventRegistry registry, EventSerializer serializer)
{
    public async Task<IReadOnlyList<EventEnvelope>> AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyList<IDomainEvent> events,
        EventMetadata metadata,
        CancellationToken ct = default,
        Guid? userId = null,
        Guid? titleId = null)
    {
        var version = expectedVersion;
        var envelopes = new List<EventEnvelope>(events.Count);

        foreach (var e in events)
        {
            version++;
            var row = new EventRow
            {
                StreamId = streamId,
                Version = version,
                EventType = registry.NameOf(e.GetType()),
                Payload = serializer.Serialize(e),
                Metadata = serializer.SerializeMetadata(metadata),
                OccurredAt = metadata.OccurredAt
            };
            db.Events.Add(row);
            envelopes.Add(new EventEnvelope(streamId, version, e, metadata, userId, titleId));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConcurrencyConflict(streamId, expectedVersion);
        }

        return envelopes;
    }

    public async Task<TState> RehydrateAsync<TState>(
        Guid streamId,
        TState initial,
        Func<TState, IDomainEvent, TState> evolve,
        CancellationToken ct = default)
    {
        var rows = await db.Events
            .AsNoTracking()
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var state = initial;
        foreach (var row in rows)
        {
            var domainEvent = serializer.Deserialize(row.EventType, row.Payload);
            state = evolve(state, domainEvent);
        }

        return state;
    }

    public async Task<(TState State, int Version)> RehydrateWithVersionAsync<TState>(
        Guid streamId,
        TState initial,
        Func<TState, IDomainEvent, TState> evolve,
        CancellationToken ct = default)
    {
        var rows = await db.Events
            .AsNoTracking()
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var state = initial;
        var version = 0;
        foreach (var row in rows)
        {
            var domainEvent = serializer.Deserialize(row.EventType, row.Payload);
            state = evolve(state, domainEvent);
            version = row.Version;
        }

        return (state, version);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
