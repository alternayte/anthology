using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public static class GetDiary
{
    public sealed record DiaryEntryDto(Guid TitleId, TrackedStatus Status, int? Rating, DateTimeOffset OccurredAt);

    public sealed class Handler(TrackingDbContext db)
    {
        public async Task<Page<DiaryEntryDto>> Handle(Guid userId, string? cursor, int size, CancellationToken ct)
        {
            size = Math.Clamp(size, 1, 100);

            var query = db.DiaryEntries.AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.TitleId);

            if (cursor is not null && TryDecodeCursor(cursor, out var cursorAt, out var cursorTitleId))
            {
                query = (IOrderedQueryable<DiaryEntry>)query.Where(e =>
                    e.OccurredAt < cursorAt ||
                    (e.OccurredAt == cursorAt && e.TitleId.CompareTo(cursorTitleId) < 0));
            }

            var items = await query
                .Take(size + 1)
                .Select(e => new DiaryEntryDto(e.TitleId, e.Status, e.Rating, e.OccurredAt))
                .ToListAsync(ct);

            string? nextCursor = null;
            if (items.Count > size)
            {
                var last = items[size - 1];
                nextCursor = EncodeCursor(last.OccurredAt, last.TitleId);
                items = items.Take(size).ToList();
            }

            return new Page<DiaryEntryDto>(items, nextCursor);
        }

        private static string EncodeCursor(DateTimeOffset at, Guid titleId) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{at:O}|{titleId}"));

        private static bool TryDecodeCursor(string cursor, out DateTimeOffset at, out Guid titleId)
        {
            at = default;
            titleId = default;
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                var parts = decoded.Split('|');
                return parts.Length == 2
                    && DateTimeOffset.TryParse(parts[0], out at)
                    && Guid.TryParse(parts[1], out titleId);
            }
            catch { return false; }
        }
    }
}
