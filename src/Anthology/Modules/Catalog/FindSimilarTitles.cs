using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Anthology.Modules.Catalog;

/// <summary>
/// Finds titles similar to a given source title.
/// <c>sourceId</c> is always excluded from results regardless of <c>excludeIds</c>.
/// <c>excludeIds</c> is an additional set of IDs to suppress (e.g. already-rendered rows).
/// Seasons and episodes are never returned.
/// </summary>
public sealed class FindSimilarTitles(CatalogDbContext db)
{
    public sealed record Row(
        Guid TitleId, string Name, int? Year, string? PosterPath, string MediaType, string[]? Genres, double? Popularity);

    public async Task<IReadOnlyList<Row>> ByEmbedding(
        Guid sourceId, Vector embedding, string mediaType, Guid[] excludeIds, int limit, CancellationToken ct)
    {
        const string sql = @"
            SELECT t.title_id, t.name, t.year, t.poster_path, t.media_type, t.genres, t.popularity
            FROM catalog.titles t
            WHERE t.title_id <> {0}
              AND t.media_type = {1}
              AND t.media_type NOT IN ('season', 'episode')
              AND t.embedding IS NOT NULL
              AND t.title_id <> ALL({2})
            ORDER BY t.embedding <=> {3}
            LIMIT {4}";

        return await db.Database
            .SqlQueryRaw<Row>(sql, sourceId, mediaType, excludeIds, embedding, limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Row>> ByOverlap(
        Guid sourceId, string[] genres, string[] keywords, string mediaType, Guid[] excludeIds, int limit, CancellationToken ct)
    {
        const string sql = @"
            SELECT t.title_id, t.name, t.year, t.poster_path, t.media_type, t.genres, t.popularity
            FROM catalog.titles t
            WHERE t.title_id <> {0}
              AND t.media_type = {2}
              AND t.media_type NOT IN ('season', 'episode')
              AND t.genres && {1}
              AND t.title_id <> ALL({3})
            ORDER BY
                (coalesce(array_length(ARRAY(SELECT unnest(t.genres) INTERSECT SELECT unnest({1})), 1), 0) * 3
               + coalesce(array_length(ARRAY(SELECT unnest(t.keywords) INTERSECT SELECT unnest({4})), 1), 0)) DESC,
                t.popularity DESC NULLS LAST
            LIMIT {5}";

        return await db.Database
            .SqlQueryRaw<Row>(sql, sourceId, genres, mediaType, excludeIds, keywords, limit)
            .ToListAsync(ct);
    }
}
