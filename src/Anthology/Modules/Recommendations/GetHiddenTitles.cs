using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Recommendations;

public static class GetHiddenTitles
{
    public sealed record HiddenTitleDto(Guid TitleId, string Name, int? Year, string? PosterPath);

    public sealed class Handler(RecommendationsDbContext db, CatalogDbContext catalog)
    {
        public async Task<IReadOnlyList<HiddenTitleDto>> Handle(Guid userId, CancellationToken ct)
        {
            var raw = await db.Feedback.AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.TitleId, f.Signal, f.CreatedAt })
                .ToListAsync(ct);

            var rows = raw.Select(r => (r.TitleId, r.Signal, r.CreatedAt));
            var excluded = FeedbackResolver.Excluded(FeedbackResolver.Resolve(rows));
            if (excluded.Count == 0)
                return [];

            var ids = excluded.ToArray();
            return await catalog.Titles.AsNoTracking()
                .Where(t => ids.Contains(t.TitleId))
                .Select(t => new HiddenTitleDto(t.TitleId, t.Name, t.Year, t.PosterPath))
                .ToListAsync(ct);
        }
    }
}
