using Anthology.Kernel;
using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public static class GetLibrary
{
    public sealed record LibraryItemDto(
        Guid TitleId, string Title, MediaType MediaType, TrackedStatus Status,
        int? Rating, DateTimeOffset AddedAt, DateTimeOffset? FinishedAt,
        int? PartsCompleted, int? PartsTotal);

    private static readonly HashSet<string> SortableFields = ["added", "finished", "rating", "title"];

    public sealed class Handler(TrackingDbContext db)
    {
        public async Task<Result<Page<LibraryItemDto>>> Handle(
            Guid userId, MediaType? media, TrackedStatus? status, int? minRating,
            string sort, string dir, string? cursor, int size, CancellationToken ct)
        {
            if (!SortableFields.Contains(sort))
                return Error.Validation("sort.unknown", $"Cannot sort by '{sort}'.");

            size = Math.Clamp(size, 1, 100);
            var descending = dir.Equals("desc", StringComparison.OrdinalIgnoreCase);

            var query = db.LibraryItems.AsNoTracking().Where(x => x.UserId == userId);

            if (media is { } m) query = query.Where(x => x.MediaType == m);
            if (status is { } s) query = query.Where(x => x.Status == s);
            if (minRating is { } r) query = query.Where(x => x.Rating >= r);

            var ordered = ApplySort(query, sort, descending);

            if (cursor is not null && TryDecodeCursor(cursor, out var cursorValues))
                ordered = (IOrderedQueryable<LibraryItem>)ApplySeek(ordered, sort, descending, cursorValues);

            var items = await ordered
                .Take(size + 1)
                .Select(x => new LibraryItemDto(
                    x.TitleId, x.Title, x.MediaType, x.Status,
                    x.Rating, x.AddedAt, x.FinishedAt,
                    x.PartsCompleted, x.PartsTotal))
                .ToListAsync(ct);

            string? nextCursor = null;
            if (items.Count > size)
            {
                nextCursor = EncodeCursor(items[size - 1], sort);
                items = items.Take(size).ToList();
            }

            return Result<Page<LibraryItemDto>>.FromValue(new Page<LibraryItemDto>(items, nextCursor));
        }

        private static IOrderedQueryable<LibraryItem> ApplySort(IQueryable<LibraryItem> q, string sort, bool desc) =>
            (sort, desc) switch
            {
                ("added", true) => q.OrderByDescending(x => x.AddedAt).ThenByDescending(x => x.TitleId),
                ("added", false) => q.OrderBy(x => x.AddedAt).ThenBy(x => x.TitleId),
                ("finished", true) => q.OrderByDescending(x => x.FinishedAt).ThenByDescending(x => x.TitleId),
                ("finished", false) => q.OrderBy(x => x.FinishedAt).ThenBy(x => x.TitleId),
                ("rating", true) => q.OrderByDescending(x => x.Rating).ThenByDescending(x => x.TitleId),
                ("rating", false) => q.OrderBy(x => x.Rating).ThenBy(x => x.TitleId),
                ("title", true) => q.OrderByDescending(x => x.Title).ThenByDescending(x => x.TitleId),
                ("title", false) => q.OrderBy(x => x.Title).ThenBy(x => x.TitleId),
                _ => q.OrderByDescending(x => x.AddedAt).ThenByDescending(x => x.TitleId)
            };

        private static IQueryable<LibraryItem> ApplySeek(
            IQueryable<LibraryItem> q, string sort, bool desc, (string sortVal, Guid tiebreaker) cursor)
        {
            return (sort, desc) switch
            {
                ("added", true) => q.Where(x =>
                    x.AddedAt < DateTimeOffset.Parse(cursor.sortVal) ||
                    (x.AddedAt == DateTimeOffset.Parse(cursor.sortVal) && x.TitleId.CompareTo(cursor.tiebreaker) < 0)),
                ("added", false) => q.Where(x =>
                    x.AddedAt > DateTimeOffset.Parse(cursor.sortVal) ||
                    (x.AddedAt == DateTimeOffset.Parse(cursor.sortVal) && x.TitleId.CompareTo(cursor.tiebreaker) > 0)),
                ("rating", true) => q.Where(x =>
                    x.Rating < int.Parse(cursor.sortVal) ||
                    (x.Rating == int.Parse(cursor.sortVal) && x.TitleId.CompareTo(cursor.tiebreaker) < 0)),
                ("rating", false) => q.Where(x =>
                    x.Rating > int.Parse(cursor.sortVal) ||
                    (x.Rating == int.Parse(cursor.sortVal) && x.TitleId.CompareTo(cursor.tiebreaker) > 0)),
                ("title", true) => q.Where(x =>
                    string.Compare(x.Title, cursor.sortVal) < 0 ||
                    (x.Title == cursor.sortVal && x.TitleId.CompareTo(cursor.tiebreaker) < 0)),
                ("title", false) => q.Where(x =>
                    string.Compare(x.Title, cursor.sortVal) > 0 ||
                    (x.Title == cursor.sortVal && x.TitleId.CompareTo(cursor.tiebreaker) > 0)),
                _ => q
            };
        }

        private static string EncodeCursor(LibraryItemDto item, string sort)
        {
            var sortVal = sort switch
            {
                "added" => item.AddedAt.ToString("O"),
                "finished" => item.FinishedAt?.ToString("O") ?? "",
                "rating" => item.Rating?.ToString() ?? "0",
                "title" => item.Title,
                _ => item.AddedAt.ToString("O")
            };
            return Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{sortVal}|{item.TitleId}"));
        }

        private static bool TryDecodeCursor(string cursor, out (string sortVal, Guid tiebreaker) result)
        {
            result = default;
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                var parts = decoded.Split('|', 2);
                if (parts.Length == 2 && Guid.TryParse(parts[1], out var tiebreaker))
                {
                    result = (parts[0], tiebreaker);
                    return true;
                }
                return false;
            }
            catch { return false; }
        }
    }
}
