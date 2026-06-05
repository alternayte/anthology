using System.Security.Claims;
using Anthology.Kernel;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public sealed record FinishRequest(int? Rating);

public sealed record RerateRequest(int Rating);

public static class TrackingEndpoints
{
    public static WebApplication MapTrackingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tracking").WithTags("Tracking");

        group.MapPost("/items/{titleId:guid}/want", async (
            Guid titleId,
            ClaimsPrincipal user,
            CatalogDbContext catalogDb,
            ICommandHandler<WantItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
        {
            var title = await catalogDb.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (title is null)
                return Results.NotFound();

            return (await handler.Handle(
                new WantItem.Command(titleId, title.Name, title.MediaType.ToString().ToLowerInvariant(),
                            user.UserId(), DateTimeOffset.UtcNow), ct)).ToHttpResult();
        })
        .RequireAuthorization();

        group.MapPost("/items/{titleId:guid}/start", async (
            Guid titleId,
            ClaimsPrincipal user,
            ICommandHandler<StartItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new StartItem.Command(DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapPost("/items/{titleId:guid}/finish", async (
            Guid titleId,
            FinishRequest request,
            ClaimsPrincipal user,
            ICommandHandler<FinishItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new FinishItem.Command(request.Rating, DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
        .RequireAuthorization();

        group.MapPost("/items/{titleId:guid}/abandon", async (
            Guid titleId,
            ClaimsPrincipal user,
            ICommandHandler<AbandonItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new AbandonItem.Command(DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapPost("/items/{titleId:guid}/rerate", async (
            Guid titleId,
            RerateRequest request,
            ClaimsPrincipal user,
            ICommandHandler<RerateItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new RerateItem.Command(request.Rating, DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapGet("/diary", async (
            ClaimsPrincipal user,
            string? cursor,
            int? size,
            GetDiary.Handler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.Handle(user.UserId(), cursor, size ?? 20, ct)))
            .RequireAuthorization();

        group.MapGet("/library", async (
            ClaimsPrincipal user,
            string? media,
            string? status,
            int? minRating,
            string? sort,
            string? dir,
            string? cursor,
            int? size,
            GetLibrary.Handler handler,
            CancellationToken ct) =>
        {
            MediaType? mediaFilter = Enum.TryParse<MediaType>(media, true, out var m) ? m : null;
            TrackedStatus? statusFilter = Enum.TryParse<TrackedStatus>(status, true, out var s) ? s : null;

            return (await handler.Handle(
                user.UserId(), mediaFilter, statusFilter, minRating,
                sort ?? "added", dir ?? "desc", cursor, size ?? 20, ct)).ToHttpResult();
        }).RequireAuthorization();

        return app;
    }
}
