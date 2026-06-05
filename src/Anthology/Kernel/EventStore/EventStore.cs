using Microsoft.EntityFrameworkCore;

namespace Anthology.Kernel.EventStore;

public sealed class EventStore(EventStoreDbContext db, EventRegistry registry, EventSerializer serializer)
{
    public async Task<IReadOnlyList<EventEnvelope>> AppendAsync<TState>(
        Guid streamId,
        string streamType,
        int expectedVersion,
        IReadOnlyList<IDomainEvent> events,
        TState newState,
        EventMetadata metadata,
        CancellationToken ct = default,
        Guid? userId = null,
        Guid? contextId = null)
    {
        var newVersion = expectedVersion + events.Count;

        if (expectedVersion == 0)
        {
            db.Streams.Add(new StreamRow
            {
                StreamId = streamId,
                StreamType = streamType,
                Version = newVersion,
                State = serializer.SerializeState(newState),
            });
        }
        else
        {
            var serializedState = serializer.SerializeState(newState);
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE es.streams SET "version" = {newVersion},
                "state" = {serializedState}::jsonb, "updated_at" = now()
                WHERE "stream_id" = {streamId} AND "version" = {expectedVersion}
                """, ct);
            if (affected == 0) throw new ConcurrencyConflict(streamId, expectedVersion);
        }

        var version = expectedVersion;
        var envelopes = new List<EventEnvelope>(events.Count);
        var enrichedMeta = metadata with { UserId = userId, ContextId = contextId };

        foreach (var e in events)
        {
            version++;
            db.Events.Add(new EventRow
            {
                StreamId = streamId,
                Version = version,
                EventType = registry.NameOf(e.GetType()),
                Payload = serializer.Serialize(e),
                Metadata = serializer.SerializeMetadata(enrichedMeta),
                OccurredAt = metadata.OccurredAt
            });
            envelopes.Add(new EventEnvelope(streamId, streamType, version, e, enrichedMeta, userId, contextId));
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    public async Task<(TState State, int Version)> LoadAsync<TState>(
        Guid streamId,
        CancellationToken ct = default)
    {
        var stream = await db.Streams.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StreamId == streamId, ct);
        if (stream is null) return (default!, 0);
        return (serializer.DeserializeState<TState>(stream.State), stream.Version);
    }
}
