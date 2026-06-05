using System.Text.Json;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Admin;

public sealed record RebuildByTypeRequest(string StreamType);

public sealed record ProjectionStatusResponse(
    string Projection,
    long Position,
    long LatestPosition,
    double Progress,
    bool IsCaughtUp);

public sealed record RebuildJobStatusResponse(
    Guid JobId,
    string StreamType,
    string Status,
    int Total,
    int Processed,
    int Failed,
    JsonElement Errors,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/streams")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapPost("/{streamId:guid}/rebuild", async (
            Guid streamId,
            StreamRebuilder rebuilder,
            CancellationToken ct) =>
            (await rebuilder.RebuildStreamAsync(streamId, ct)).ToHttpResult());

        group.MapPost("/rebuild", async (
            RebuildByTypeRequest request,
            StreamRebuilder rebuilder,
            CancellationToken ct) =>
        {
            var result = await rebuilder.CreateJobAsync(request.StreamType, ct);
            return result.Match(
                jobId => Results.Accepted(
                    $"/admin/streams/rebuild/{jobId}",
                    new { jobId }),
                err => err.Kind switch
                {
                    ErrorKind.Unprocessable => Results.Problem(
                        err.Message, statusCode: 422, title: err.Code),
                    _ => Results.Problem(err.Message, statusCode: 500)
                });
        });

        group.MapGet("/rebuild/{jobId:guid}", async (
            Guid jobId,
            EventStoreDbContext db,
            CancellationToken ct) =>
        {
            var job = await db.RebuildJobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, ct);

            if (job is null) return Results.NotFound();

            return Results.Ok(new RebuildJobStatusResponse(
                job.Id, job.StreamType, job.Status,
                job.Total, job.Processed, job.Failed,
                JsonSerializer.Deserialize<JsonElement>(job.Errors),
                job.StartedAt, job.CompletedAt));
        });

        var projections = app.MapGroup("/admin/projections")
            .WithTags("Admin")
            .RequireAuthorization();

        projections.MapGet("/", async (
            AsyncProjectionRegistry registry,
            EventStoreDbContext db,
            CancellationToken ct) =>
        {
            var latestPosition = await db.Events.AsNoTracking()
                .MaxAsync(e => (long?)e.GlobalPosition, ct) ?? 0;

            var checkpoints = await db.Checkpoints.AsNoTracking()
                .Where(c => registry.ProjectionNames.Contains(c.ProjectionName))
                .ToDictionaryAsync(c => c.ProjectionName, ct);

            var results = registry.ProjectionNames.Select(name =>
            {
                var position = checkpoints.TryGetValue(name, out var cp) ? cp.Position : 0;
                var progress = latestPosition > 0 ? (double)position / latestPosition : 1.0;
                return new ProjectionStatusResponse(name, position, latestPosition, Math.Round(progress, 4), position >= latestPosition);
            });

            return Results.Ok(results);
        });

        projections.MapGet("/{name}/status", async (
            string name,
            AsyncProjectionRegistry registry,
            EventStoreDbContext db,
            CancellationToken ct) =>
        {
            if (!registry.ProjectionNames.Contains(name))
                return Results.NotFound();

            var latestPosition = await db.Events.AsNoTracking()
                .MaxAsync(e => (long?)e.GlobalPosition, ct) ?? 0;

            var checkpoint = await db.Checkpoints.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ProjectionName == name, ct);

            var position = checkpoint?.Position ?? 0;
            var progress = latestPosition > 0 ? (double)position / latestPosition : 1.0;

            return Results.Ok(new ProjectionStatusResponse(name, position, latestPosition, Math.Round(progress, 4), position >= latestPosition));
        });

        projections.MapPost("/{name}/rebuild", async (
            string name,
            AsyncProjectionRegistry registry,
            EventStoreDbContext db,
            CancellationToken ct) =>
        {
            if (!registry.ProjectionNames.Contains(name))
                return Results.NotFound();

            var projectionType = registry.ProjectionTypes
                .First(t => t.Name == name);

            if (!projectionType.GetInterfaces().Contains(typeof(IRebuildableProjection)))
                return Results.Problem("Projection does not support rebuild.", statusCode: 422);

            var tableName = (string)projectionType
                .GetProperty("SchemaQualifiedTableName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;

            await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tableName}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE es.checkpoints SET "position" = 0, "updated_at" = now()
                WHERE "projection_name" = {name}
                """, ct);

            return Results.Accepted(value: new { projection = name });
        });

        return app;
    }
}
