using System.Security.Claims;
using Anthology.Kernel;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public sealed record FinishRequest(int? Rating);

public sealed record RerateRequest(int Rating);

public sealed record CreateListRequest(string Name, string? Description, string? Visibility);

public sealed record UpdateListRequest(string? Name, string? Description, string? Visibility);

public sealed record AddItemToListRequest(Guid TitleId);

public sealed record ReorderItemRequest(Guid? AfterTitleId);

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
        .RequireAuthorization().WithName("wantItem").Produces<TrackedItemDto>();

        group.MapPost("/items/{titleId:guid}/start", async (
            Guid titleId,
            ClaimsPrincipal user,
            ICommandHandler<StartItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new StartItem.Command(DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization().WithName("startItem").Produces<TrackedItemDto>();

        group.MapPost("/items/{titleId:guid}/finish", async (
            Guid titleId,
            FinishRequest request,
            ClaimsPrincipal user,
            ICommandHandler<FinishItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new FinishItem.Command(request.Rating, DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
        .RequireAuthorization().WithName("finishItem").Produces<TrackedItemDto>();

        group.MapPost("/items/{titleId:guid}/abandon", async (
            Guid titleId,
            ClaimsPrincipal user,
            ICommandHandler<AbandonItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new AbandonItem.Command(DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization().WithName("abandonItem").Produces<TrackedItemDto>();

        group.MapPost("/items/{titleId:guid}/rerate", async (
            Guid titleId,
            RerateRequest request,
            ClaimsPrincipal user,
            ICommandHandler<RerateItem.Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new RerateItem.Command(request.Rating, DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .RequireAuthorization().WithName("rerateItem").Produces<TrackedItemDto>();

        group.MapGet("/diary", async (
            ClaimsPrincipal user,
            string? cursor,
            int? size,
            GetDiary.Handler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.Handle(user.UserId(), cursor, size ?? 20, ct)))
            .RequireAuthorization().WithName("getDiary").Produces<Page<GetDiary.DiaryEntryDto>>();

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
        }).RequireAuthorization().WithName("getLibrary").Produces<Page<GetLibrary.LibraryItemDto>>();

        var lists = group.MapGroup("/lists").WithTags("Lists");

        lists.MapPost("/", async (
            CreateListRequest request,
            ClaimsPrincipal user,
            ICommandHandler<CreateList.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
        {
            var visibility = Enum.TryParse<ListVisibility>(request.Visibility, true, out var v)
                ? v : ListVisibility.Private;
            return (await handler.Handle(
                new CreateList.Command(request.Name, request.Description, visibility,
                    user.UserId(), Guid.NewGuid(), DateTimeOffset.UtcNow), ct)).ToHttpResult();
        }).RequireAuthorization().WithName("createList").Produces<CuratedListDto>();

        lists.MapPut("/{listId:guid}", async (
            Guid listId,
            UpdateListRequest request,
            ClaimsPrincipal user,
            ICommandHandler<UpdateList.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
        {
            ListVisibility? visibility = Enum.TryParse<ListVisibility>(request.Visibility, true, out var v)
                ? v : null;
            var descProvided = request.Description is not null;
            var desc = request.Description == "" ? null : request.Description;
            return (await handler.Handle(
                new UpdateList.Command(request.Name, desc, descProvided, visibility,
                    user.UserId(), listId, DateTimeOffset.UtcNow), ct)).ToHttpResult();
        }).RequireAuthorization().WithName("updateList").Produces<CuratedListDto>();

        lists.MapDelete("/{listId:guid}", async (
            Guid listId,
            ClaimsPrincipal user,
            ICommandHandler<DeleteList.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(
                new DeleteList.Command(user.UserId(), listId, DateTimeOffset.UtcNow), ct)).ToHttpResult())
            .RequireAuthorization().WithName("deleteList").Produces<CuratedListDto>();

        lists.MapPost("/{listId:guid}/items", async (
            Guid listId,
            AddItemToListRequest request,
            ClaimsPrincipal user,
            ICommandHandler<AddItemToList.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(
                new AddItemToList.Command(request.TitleId, user.UserId(), listId, DateTimeOffset.UtcNow), ct)).ToHttpResult())
            .RequireAuthorization().WithName("addItemToList").Produces<CuratedListDto>();

        lists.MapDelete("/{listId:guid}/items/{titleId:guid}", async (
            Guid listId,
            Guid titleId,
            ClaimsPrincipal user,
            ICommandHandler<RemoveItemFromList.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(
                new RemoveItemFromList.Command(titleId, user.UserId(), listId, DateTimeOffset.UtcNow), ct)).ToHttpResult())
            .RequireAuthorization().WithName("removeItemFromList").Produces<CuratedListDto>();

        lists.MapPut("/{listId:guid}/items/{titleId:guid}/position", async (
            Guid listId,
            Guid titleId,
            ReorderItemRequest request,
            ClaimsPrincipal user,
            ICommandHandler<ReorderItem.Command, Result<CuratedListDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(
                new ReorderItem.Command(titleId, request.AfterTitleId, user.UserId(), listId, DateTimeOffset.UtcNow), ct)).ToHttpResult())
            .RequireAuthorization().WithName("reorderItem").Produces<CuratedListDto>();

        lists.MapGet("/", async (
            ClaimsPrincipal user,
            GetUserLists.Handler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.Handle(user.UserId(), ct)))
            .RequireAuthorization().WithName("getUserLists").Produces<IReadOnlyList<GetUserLists.ListSummaryDto>>();

        lists.MapGet("/{listId:guid}", async (
            Guid listId,
            ClaimsPrincipal? user,
            GetList.Handler handler,
            CancellationToken ct) =>
        {
            Guid? requestingUserId = null;
            try { requestingUserId = user?.UserId(); } catch { }
            return (await handler.Handle(listId, requestingUserId, ct)).ToHttpResult();
        }).WithName("getList").Produces<GetList.ListDetailDto>();

        return app;
    }
}
