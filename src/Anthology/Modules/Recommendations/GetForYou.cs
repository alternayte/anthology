using System.Text.Json;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Anthology.Modules.Recommendations;

public static class GetForYou
{
    private const int MinRatingForSeed = 8;
    private const int MaxRows = 6;
    private const int MinSeedsForPersonalized = 3;
    private const float DiversityDistanceThreshold = 0.15f;
    private const int PerSeedFetch = 20;
    private const int ItemsPerRow = 12;

    public sealed record FeedItemDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType, string[]? Genres);

    public sealed record FeedRowDto(Guid SeedTitleId, string SeedName, IReadOnlyList<FeedItemDto> Items);

    private sealed record Seed(Guid TitleId, string Name, DateTimeOffset Recency);

    private sealed record SeedSource(
        Guid TitleId, MediaType MediaType, Vector? Embedding, string[]? Genres, string[]? Keywords);

    // snake_case media string ("tv_show") → MediaType enum. Enum.Parse throws on multi-word values,
    // so look up via a map keyed exactly as the persisted snake_case string, with Film as the fallback.
    private static readonly Dictionary<string, MediaType> MediaTypeMap =
        Enum.GetValues<MediaType>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    private static MediaType ToMediaType(string snake) => MediaTypeMap.GetValueOrDefault(snake, MediaType.Film);

    private static string ToSnake(MediaType mediaType) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(mediaType.ToString());

    public sealed class Handler(
        TrackingDbContext tracking, CatalogDbContext catalog, RecommendationsDbContext recs, FindSimilarTitles similar)
    {
        public async Task<IReadOnlyList<FeedRowDto>> Handle(Guid userId, CancellationToken ct)
        {
            // 1. Resolve feedback state for this user.
            // Materialize to an anonymous type first, then project to tuples in memory —
            // EF can't reliably translate a ValueTuple constructor inside .Select(...).
            var feedbackRaw = await recs.Feedback.AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.TitleId, f.Signal, f.CreatedAt })
                .ToListAsync(ct);

            var resolved = FeedbackResolver.Resolve(
                feedbackRaw.Select(r => (r.TitleId, r.Signal, r.CreatedAt)));
            var excludedByFeedback = FeedbackResolver.Excluded(resolved);
            var promoted = FeedbackResolver.Promoted(resolved);

            // 2. seenIds = every title in the user's library, regardless of status.
            var libraryItems = await tracking.LibraryItems.AsNoTracking()
                .Where(li => li.UserId == userId)
                .Select(li => new { li.TitleId, li.Title, li.Rating, li.AddedAt, li.FinishedAt })
                .ToListAsync(ct);

            var seenIds = libraryItems.Select(li => li.TitleId).ToHashSet();

            // 3. Candidate seeds: highly-rated library items ∪ promoted (MoreLikeThis) titles.
            var seeds = new Dictionary<Guid, Seed>();

            foreach (var li in libraryItems.Where(li => li.Rating >= MinRatingForSeed))
                seeds[li.TitleId] = new Seed(li.TitleId, li.Title, li.FinishedAt ?? li.AddedAt);

            if (promoted.Count > 0)
            {
                // Recency for a promoted seed = the most recent of its MoreLikeThis feedback rows.
                var promotedRecency = feedbackRaw
                    .Where(r => r.Signal == FeedbackSignal.MoreLikeThis && promoted.Contains(r.TitleId))
                    .GroupBy(r => r.TitleId)
                    .ToDictionary(g => g.Key, g => g.Max(r => r.CreatedAt));

                var promotedNames = await catalog.Titles.AsNoTracking()
                    .Where(t => promoted.Contains(t.TitleId))
                    .Select(t => new { t.TitleId, t.Name })
                    .ToListAsync(ct);

                foreach (var p in promotedNames)
                {
                    var recency = promotedRecency.GetValueOrDefault(p.TitleId, DateTimeOffset.MinValue);
                    // A promoted title may also be a rated library item; keep the more recent signal.
                    if (seeds.TryGetValue(p.TitleId, out var existing))
                        recency = existing.Recency > recency ? existing.Recency : recency;
                    seeds[p.TitleId] = new Seed(p.TitleId, p.Name, recency);
                }
            }

            // All titles that became seeds (rated ∪ promoted, before diversity filtering).
            // Used to prevent any seed title from appearing as a recommended item in another seed's row.
            var seedIds = seeds.Keys.ToHashSet();

            // 4. Cold start: not enough personalized signal → a single "Popular right now" row.
            if (seeds.Count < MinSeedsForPersonalized)
                return await ColdStart(seenIds, excludedByFeedback, ct);

            // 5. Diversity guard: order by recency desc, greedily drop near-duplicate seeds by embedding distance.
            var orderedSeeds = seeds.Values.OrderByDescending(s => s.Recency).ToList();
            var orderedSeedIds = orderedSeeds.Select(s => s.TitleId).ToArray();

            var seedEmbeddings = await catalog.Titles.AsNoTracking()
                .Where(t => orderedSeedIds.Contains(t.TitleId) && t.Embedding != null)
                .Select(t => new { t.TitleId, t.Embedding })
                .ToListAsync(ct);

            var embeddingByTitle = seedEmbeddings.ToDictionary(
                t => t.TitleId, t => t.Embedding!.ToArray());

            var keptSeeds = new List<Seed>();
            var keptEmbeddings = new List<float[]>();
            foreach (var seed in orderedSeeds)
            {
                if (keptSeeds.Count >= MaxRows) break;

                if (embeddingByTitle.TryGetValue(seed.TitleId, out var embedding))
                {
                    var tooClose = keptEmbeddings.Any(kept =>
                        VectorMath.CosineDistance(embedding, kept) < DiversityDistanceThreshold);
                    if (tooClose) continue;
                    keptEmbeddings.Add(embedding);
                }

                // Seeds without an embedding can't be measured, so we always keep them.
                keptSeeds.Add(seed);
            }

            // 6. Per-seed rows. Each title lands in at most one row (cross-row dedup via `placed`).
            // Fetch the source data for all kept seeds in one query, projecting only the columns the loop uses.
            var keptSeedIds = keptSeeds.Select(s => s.TitleId).ToArray();
            var sourceById = (await catalog.Titles.AsNoTracking()
                    .Where(t => keptSeedIds.Contains(t.TitleId))
                    .Select(t => new SeedSource(t.TitleId, t.MediaType, t.Embedding, t.Genres, t.Keywords))
                    .ToListAsync(ct))
                .ToDictionary(s => s.TitleId);

            var placed = new HashSet<Guid>();
            var rows = new List<FeedRowDto>();

            foreach (var seed in keptSeeds)
            {
                if (!sourceById.TryGetValue(seed.TitleId, out var source)) continue;

                var excludeIds = seenIds
                    .Concat(excludedByFeedback)
                    .Concat(seedIds)
                    .Concat(placed)
                    .Distinct()
                    .ToArray();

                var mediaStr = ToSnake(source.MediaType);

                var candidates = source.Embedding is not null
                    ? await similar.ByEmbedding(source.TitleId, source.Embedding, mediaStr, excludeIds, PerSeedFetch, ct)
                    : await similar.ByOverlap(
                        source.TitleId, source.Genres ?? [], source.Keywords ?? [], mediaStr, excludeIds, PerSeedFetch, ct);

                var items = new List<FeedItemDto>();
                foreach (var c in candidates)
                {
                    if (items.Count >= ItemsPerRow) break;
                    if (!placed.Add(c.TitleId)) continue;

                    items.Add(new FeedItemDto(
                        c.TitleId, c.Name, c.Year, c.PosterPath, ToMediaType(c.MediaType), c.Genres));
                }

                if (items.Count > 0)
                    rows.Add(new FeedRowDto(seed.TitleId, seed.Name, items));
            }

            return rows;
        }

        private async Task<IReadOnlyList<FeedRowDto>> ColdStart(
            IReadOnlySet<Guid> seenIds, IReadOnlySet<Guid> excludedByFeedback, CancellationToken ct)
        {
            var excluded = seenIds.Concat(excludedByFeedback).Distinct().ToArray();

            var popular = await catalog.Titles.AsNoTracking()
                .Where(t => t.MediaType == MediaType.Film && !excluded.Contains(t.TitleId))
                .OrderByDescending(t => t.Popularity)
                .Take(ItemsPerRow)
                .Select(t => new FeedItemDto(t.TitleId, t.Name, t.Year, t.PosterPath, t.MediaType, t.Genres))
                .ToListAsync(ct);

            return popular.Count == 0
                ? []
                : [new FeedRowDto(Guid.Empty, "Popular right now", popular)];
        }
    }
}
