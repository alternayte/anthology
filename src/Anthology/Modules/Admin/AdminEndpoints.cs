using System.Text.Json;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Admin;

public sealed record RebuildByTypeRequest(string StreamType);

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
                JsonDocument.Parse(job.Errors).RootElement,
                job.StartedAt, job.CompletedAt));
        });

        return app;
    }
}
