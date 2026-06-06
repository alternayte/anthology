using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public static class GetList
{
    public sealed record ListDetailDto(
        Guid ListId, string Name, string? Description, ListVisibility Visibility,
        Guid UserId, DateTimeOffset CreatedAt, IReadOnlyList<ListItemDto> Items);

    public sealed record ListItemDto(
        Guid TitleId, string Title, string MediaType, string? PosterPath,
        double Position, DateTimeOffset AddedAt);

    public sealed class Handler(TrackingDbContext db)
    {
        public async Task<Result<ListDetailDto>> Handle(Guid listId, Guid? requestingUserId, CancellationToken ct)
        {
            var list = await db.Lists.AsNoTracking()
                .FirstOrDefaultAsync(l => l.ListId == listId && !l.IsDeleted, ct);

            if (list is null)
                return Error.NotFound("lists.not_found", "List not found.");

            if (list.Visibility == ListVisibility.Private && requestingUserId != list.UserId)
                return Error.NotFound("lists.not_found", "List not found.");

            var items = await db.ListItems.AsNoTracking()
                .Where(i => i.ListId == listId)
                .OrderBy(i => i.Position)
                .Select(i => new ListItemDto(
                    i.TitleId, i.Title, i.MediaType, i.PosterPath,
                    i.Position, i.AddedAt))
                .ToListAsync(ct);

            return Result<ListDetailDto>.FromValue(new ListDetailDto(
                list.ListId, list.Name, list.Description, list.Visibility,
                list.UserId, list.CreatedAt, items));
        }
    }
}
