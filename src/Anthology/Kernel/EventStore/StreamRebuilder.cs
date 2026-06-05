using Microsoft.EntityFrameworkCore;

namespace Anthology.Kernel.EventStore;

public sealed record RebuildStreamResult(Guid StreamId, int EventsReplayed);

public sealed class StreamRebuilder(
    EventStoreDbContext db,
    StreamEvolverRegistry evolverRegistry)
{
    public async Task<Result<RebuildStreamResult>> RebuildStreamAsync(
        Guid streamId, CancellationToken ct = default)
    {
        var stream = await db.Streams
            .FirstOrDefaultAsync(s => s.StreamId == streamId, ct);

        if (stream is null)
            return Error.NotFound("rebuild.stream_not_found",
                $"Stream '{streamId}' not found.");

        if (!evolverRegistry.IsRegistered(stream.StreamType))
            return Error.Unprocessable("rebuild.no_rebuilder",
                $"No rebuilder registered for stream type '{stream.StreamType}'.");

        var events = await db.Events
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .AsNoTracking()
            .ToListAsync(ct);

        var rebuilder = evolverRegistry.GetRebuilder(stream.StreamType);
        stream.State = rebuilder(events);
        stream.Version = events.Count;
        stream.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new RebuildStreamResult(streamId, events.Count);
    }

    public async Task<Result<Guid>> CreateJobAsync(
        string streamType, CancellationToken ct = default)
    {
        if (!evolverRegistry.IsRegistered(streamType))
            return Error.Unprocessable("rebuild.no_rebuilder",
                $"No rebuilder registered for stream type '{streamType}'.");

        var total = await db.Streams
            .CountAsync(s => s.StreamType == streamType, ct);

        var job = new RebuildJobRow
        {
            Id = Guid.NewGuid(),
            StreamType = streamType,
            Total = total
        };

        db.RebuildJobs.Add(job);
        await db.SaveChangesAsync(ct);

        return job.Id;
    }
}
