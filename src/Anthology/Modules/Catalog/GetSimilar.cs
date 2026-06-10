using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Anthology.Modules.Catalog;

public static class GetSimilar
{
    public sealed record SimilarTitleDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType, string[]? Genres);

    // Internal DTO matching raw SQL column types (no value converters applied)
    private sealed record RawRow(
        Guid TitleId, string Name, int? Year, string? PosterPath, string MediaType, string[]? Genres);

    private static readonly Dictionary<string, MediaType> MediaTypeMap =
        Enum.GetValues<MediaType>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    public sealed class Handler(CatalogDbContext db)
    {
        public async Task<IReadOnlyList<SimilarTitleDto>> Handle(Guid titleId, CancellationToken ct)
        {
            var source = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (source is null)
                return [];

            var mediaTypeStr = JsonNamingPolicy.SnakeCaseLower.ConvertName(source.MediaType.ToString());
            var excludeSeason = JsonNamingPolicy.SnakeCaseLower.ConvertName(Catalog.MediaType.Season.ToString());
            var excludeEpisode = JsonNamingPolicy.SnakeCaseLower.ConvertName(Catalog.MediaType.Episode.ToString());

            List<RawRow> rows;

            if (source.Embedding is not null)
            {
                rows = await FindByEmbedding(source.TitleId, source.Embedding, mediaTypeStr, excludeSeason, excludeEpisode, ct);
            }
            else
            {
                rows = await FindByOverlap(source.TitleId, source.Genres ?? [], source.Keywords ?? [], mediaTypeStr, excludeSeason, excludeEpisode, ct);
            }

            return rows.Select(r => new SimilarTitleDto(
                r.TitleId, r.Name, r.Year, r.PosterPath,
                MediaTypeMap.GetValueOrDefault(r.MediaType, Catalog.MediaType.Film),
                r.Genres)).ToList();
        }

        private async Task<List<RawRow>> FindByEmbedding(
            Guid sourceId, Vector embedding, string mediaType,
            string excludeSeason, string excludeEpisode, CancellationToken ct)
        {
            var sql = @"
                SELECT
                    t.title_id,
                    t.name,
                    t.year,
                    t.poster_path,
                    t.media_type,
                    t.genres
                FROM catalog.titles t
                WHERE t.title_id <> {0}
                  AND t.media_type = {1}
                  AND t.media_type NOT IN ({2}, {3})
                  AND t.embedding IS NOT NULL
                ORDER BY t.embedding <=> {4}
                LIMIT 12";

            return await db.Database
                .SqlQueryRaw<RawRow>(sql, sourceId, mediaType, excludeSeason, excludeEpisode, embedding)
                .ToListAsync(ct);
        }

        private async Task<List<RawRow>> FindByOverlap(
            Guid sourceId, string[] genres, string[] keywords, string mediaType,
            string excludeSeason, string excludeEpisode, CancellationToken ct)
        {
            // Genre overlap weighted 3x keyword overlap, require at least one genre in common
            var sql = @"
                SELECT
                    t.title_id,
                    t.name,
                    t.year,
                    t.poster_path,
                    t.media_type,
                    t.genres
                FROM catalog.titles t
                WHERE t.title_id <> {0}
                  AND t.media_type = {1}
                  AND t.media_type NOT IN ({2}, {3})
                  AND t.genres && {4}
                ORDER BY
                    (coalesce(array_length(ARRAY(SELECT unnest(t.genres) INTERSECT SELECT unnest({4})), 1), 0) * 3
                   + coalesce(array_length(ARRAY(SELECT unnest(t.keywords) INTERSECT SELECT unnest({5})), 1), 0)) DESC,
                    t.popularity DESC NULLS LAST
                LIMIT 12";

            return await db.Database
                .SqlQueryRaw<RawRow>(sql, sourceId, mediaType, excludeSeason, excludeEpisode, genres, keywords)
                .ToListAsync(ct);
        }
    }
}
