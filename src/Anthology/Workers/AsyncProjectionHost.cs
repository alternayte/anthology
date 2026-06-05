using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Anthology.Workers;

public sealed class AsyncProjectionHost(
    IServiceScopeFactory scopeFactory,
    AsyncProjectionRegistry registry,
    NpgsqlDataSource dataSource,
    ILogger<AsyncProjectionHost> log) : BackgroundService
{
    public override async Task StartAsync(CancellationToken ct)
    {
        if (registry.ProjectionNames.Count > 0)
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            foreach (var name in registry.ProjectionNames)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO es.checkpoints (\"projection_name\", \"position\") VALUES ($1, 0) ON CONFLICT DO NOTHING";
                cmd.Parameters.AddWithValue(name);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (registry.ProjectionTypes.Count == 0) return;

        var tasks = registry.ProjectionTypes.Select(type => RunProjection(type, ct));
        await Task.WhenAll(tasks);
    }

    private async Task RunProjection(Type projectionType, CancellationToken ct)
    {
        var name = projectionType.Name;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessBatch(projectionType, name, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing projection {Projection}, retrying in 5s", name);
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task ProcessBatch(Type projectionType, string name, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var projection = (IProjection)scope.ServiceProvider.GetRequiredService(projectionType);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var checkpoint = await db.Checkpoints
            .FromSqlInterpolated($"""
                SELECT * FROM es.checkpoints
                WHERE "projection_name" = {name}
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(ct);

        if (checkpoint is null)
        {
            await tx.RollbackAsync(ct);
            await Task.Delay(1000, ct);
            return;
        }

        var batch = await db.Events
            .FromSqlInterpolated($"""
                SELECT * FROM es.events
                WHERE "global_position" > {checkpoint.Position}
                  AND "xid" < pg_snapshot_xmin(pg_current_snapshot())
                ORDER BY "global_position"
                LIMIT 500
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            await tx.CommitAsync(ct);
            await WaitForNotify(ct);
            return;
        }

        var serializer = scope.ServiceProvider.GetRequiredService<EventSerializer>();
        var envelopes = batch.Select(row =>
        {
            var domainEvent = serializer.Deserialize(row.EventType, row.Payload);
            var metadata = serializer.DeserializeMetadata(row.Metadata);
            return new EventEnvelope(row.StreamId, string.Empty, row.Version, domainEvent, metadata);
        }).ToList();

        if (projection is IDbContextProjection contextProjection)
            await contextProjection.DbContext.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);

        await projection.ApplyAsync(envelopes, ct);

        if (projection is IDbContextProjection dbProjection)
            await dbProjection.DbContext.SaveChangesAsync(ct);

        checkpoint.Position = batch[^1].GlobalPosition;
        checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogDebug("{Projection} caught up to {Position}", name, checkpoint.Position);
    }

    private async Task WaitForNotify(CancellationToken ct)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var listenCmd = conn.CreateCommand();
            listenCmd.CommandText = "LISTEN new_events";
            await listenCmd.ExecuteNonQueryAsync(ct);
            await conn.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await Task.Delay(1000, ct);
        }
    }
}
