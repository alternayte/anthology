using System.Text.Json;
using Anthology.Modules.Tracking;
using Deedbox;

namespace Anthology.Modules.Admin;

public sealed record RebuildByTypeRequest(string StreamType);

public sealed record ProjectionStatusResponse(
    string Projection,
    string Mode,
    string Status,
    long Position,
    long Lag,
    bool IsCaughtUp,
    string? Error);

public sealed record JobStatusResponse(
    Guid JobId,
    string Kind,
    string Status,
    JsonElement Args,
    JsonElement? Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public sealed record RebuildJobStatusResponse(
    Guid JobId,
    string StreamType,
    string Status,
    JsonElement? Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/streams")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapGet("/types", () => Results.Ok(TrackingModule.StreamTypes));

        group.MapPost("/rebuild", async (
            RebuildByTypeRequest request,
            IEventStoreAdmin admin,
            CancellationToken ct) =>
        {
            try
            {
                var jobId = await admin.RebuildSnapshotsAsync(request.StreamType, ct);
                return Results.Accepted($"/admin/streams/rebuild/{jobId}", new { jobId });
            }
            catch (DeedboxException ex)
            {
                return Results.Problem(ex.Message, statusCode: 422, title: ex.Code);
            }
        });

        group.MapGet("/rebuild/{jobId:guid}", async (
            Guid jobId,
            IEventStoreAdmin admin,
            CancellationToken ct) =>
        {
            var job = await admin.GetJobAsync(jobId, ct);
            if (job is null) return Results.NotFound();

            var args = JsonDocument.Parse(job.Args).RootElement;
            var streamType = args.TryGetProperty("streamType", out var type) ? type.GetString() : null;
            if (streamType is null) return Results.NotFound();

            return Results.Ok(new RebuildJobStatusResponse(
                job.Id, streamType, job.Status, Json(job.Progress), job.CreatedAt, job.FinishedAt));
        });

        var projections = app.MapGroup("/admin/projections")
            .WithTags("Admin")
            .RequireAuthorization();

        projections.MapGet("/", async (IEventStoreAdmin admin, CancellationToken ct) =>
        {
            var status = await admin.GetStatusAsync(ct);
            return Results.Ok(status.Consumers.Select(Projection));
        });

        projections.MapGet("/{name}/status", async (
            string name,
            IEventStoreAdmin admin,
            CancellationToken ct) =>
        {
            var status = await admin.GetStatusAsync(ct);
            var consumer = status.Consumers.FirstOrDefault(c => c.Name == name);
            return consumer is null ? Results.NotFound() : Results.Ok(Projection(consumer));
        });

        projections.MapPost("/{name}/rebuild", async (
            string name,
            IEventStoreAdmin admin,
            CancellationToken ct) =>
        {
            try
            {
                var jobId = await admin.RebuildAsync(name, ct);
                return Results.Accepted($"/admin/jobs/{jobId}", new { projection = name, jobId });
            }
            catch (DeedboxException)
            {
                return Results.NotFound();
            }
        });

        app.MapGroup("/admin/jobs")
            .WithTags("Admin")
            .RequireAuthorization()
            .MapGet("/{jobId:guid}", async (
                Guid jobId,
                IEventStoreAdmin admin,
                CancellationToken ct) =>
            {
                var job = await admin.GetJobAsync(jobId, ct);
                return job is null
                    ? Results.NotFound()
                    : Results.Ok(new JobStatusResponse(
                        job.Id, job.Kind, job.Status, JsonDocument.Parse(job.Args).RootElement,
                        Json(job.Progress), job.CreatedAt, job.FinishedAt));
            });

        return app;
    }

    private static ProjectionStatusResponse Projection(ConsumerStatus c) =>
        new(c.Name, c.Mode, c.Status, c.Position, c.Lag, c.Status == "running" && c.Lag == 0, c.Error);

    private static JsonElement? Json(string? json) =>
        json is null ? null : JsonDocument.Parse(json).RootElement;
}
