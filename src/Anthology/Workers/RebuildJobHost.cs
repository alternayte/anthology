using System.Text.Json;
using Anthology.Kernel.EventStore;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Workers;

public sealed class RebuildJobHost(
    IServiceScopeFactory scopeFactory,
    StreamEvolverRegistry evolverRegistry,
    ILogger<RebuildJobHost> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextJobAsync(ct);
                if (!processed)
                    await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing rebuild job, retrying in 5s");
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task<bool> ProcessNextJobAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var job = await db.RebuildJobs
            .FromSqlInterpolated($"""
                SELECT * FROM es.rebuild_jobs
                WHERE "status" = 'pending'
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        job.Status = "running";
        job.StartedAt = DateTimeOffset.UtcNow;

        var streamIds = await db.Streams
            .Where(s => s.StreamType == job.StreamType)
            .Select(s => s.StreamId)
            .ToListAsync(ct);

        var errors = new List<object>();

        foreach (var streamId in streamIds)
        {
            try
            {
                var stream = await db.Streams
                    .FirstAsync(s => s.StreamId == streamId, ct);
                var events = await db.Events
                    .Where(e => e.StreamId == streamId)
                    .OrderBy(e => e.Version)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var rebuilder = evolverRegistry.GetRebuilder(stream.StreamType);
                stream.State = rebuilder(events);
                stream.Version = events.Count > 0 ? events[^1].Version : 0;
                stream.UpdatedAt = DateTimeOffset.UtcNow;
                job.Processed++;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to rebuild stream {StreamId}", streamId);
                job.Failed++;
                job.Processed++;
                errors.Add(new { streamId, error = ex.Message });
            }
        }

        job.Status = "completed";
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Errors = JsonSerializer.Serialize(errors, EventSerializer.Options);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogInformation(
            "Rebuild job {JobId} completed: {Processed} processed, {Failed} failed",
            job.Id, job.Processed, job.Failed);

        return true;
    }
}
