using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public static class GetUserLists
{
    public sealed record ListSummaryDto(
        Guid ListId, string Name, string? Description, ListVisibility Visibility,
        int ItemCount, DateTimeOffset CreatedAt);

    public sealed class Handler(TrackingDbContext db)
    {
        public async Task<IReadOnlyList<ListSummaryDto>> Handle(Guid userId, CancellationToken ct)
        {
            return await db.Lists.AsNoTracking()
                .Where(l => l.UserId == userId && !l.IsDeleted)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new ListSummaryDto(
                    l.ListId, l.Name, l.Description, l.Visibility,
                    l.ItemCount, l.CreatedAt))
                .ToListAsync(ct);
        }
    }
}
