using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class SearchLocal
{
    public sealed record Query(string Term, MediaType? MediaType, Guid? UserId);

    public sealed record LocalSearchResult(
        Guid TitleId, string Name, int? Year, string? PosterPath, string? Overview,
        MediaType MediaType, string[]? Genres, double Score);

    // Internal DTO matching raw SQL column types (no value converters applied)
    private sealed record RawRow(
        Guid TitleId, string Name, int? Year, string? PosterPath, string? Overview,
        string MediaType, string[]? Genres, double Score);

    private static readonly Dictionary<string, MediaType> MediaTypeMap =
        Enum.GetValues<MediaType>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    public sealed class Handler(CatalogDbContext db)
    {
        public async Task<IReadOnlyList<LocalSearchResult>> Handle(Query query, CancellationToken ct)
        {
            var trimmed = query.Term.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return [];

            var tsquery = string.Join(" & ", trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var excludeSeason = JsonNamingPolicy.SnakeCaseLower.ConvertName(Catalog.MediaType.Season.ToString());
            var excludeEpisode = JsonNamingPolicy.SnakeCaseLower.ConvertName(Catalog.MediaType.Episode.ToString());

            var parameters = new List<object> { tsquery, excludeSeason, excludeEpisode };
            var mediaFilter = "";

            if (query.MediaType.HasValue)
            {
                var mediaStr = JsonNamingPolicy.SnakeCaseLower.ConvertName(query.MediaType.Value.ToString());
                mediaFilter = "AND t.media_type = {3}";
                parameters.Add(mediaStr);
            }

            var sql = $@"
                SELECT
                    t.title_id,
                    t.name,
                    t.year,
                    t.poster_path,
                    t.overview,
                    t.media_type,
                    t.genres,
                    ts_rank(to_tsvector('english', coalesce(t.name, '') || ' ' || coalesce(t.overview, '')),
                            to_tsquery('english', {{0}}))
                        * (1.0 + ln(coalesce(t.popularity, 0) + 1) * 0.1) AS score
                FROM catalog.titles t
                WHERE to_tsvector('english', coalesce(t.name, '') || ' ' || coalesce(t.overview, ''))
                      @@ to_tsquery('english', {{0}})
                  AND t.media_type NOT IN ({{1}}, {{2}})
                  {mediaFilter}
                ORDER BY score DESC
                LIMIT 20";

            var rows = await db.Database
                .SqlQueryRaw<RawRow>(sql, parameters.ToArray())
                .ToListAsync(ct);

            return rows.Select(r => new LocalSearchResult(
                r.TitleId, r.Name, r.Year, r.PosterPath, r.Overview,
                MediaTypeMap.GetValueOrDefault(r.MediaType, Catalog.MediaType.Film),
                r.Genres, r.Score)).ToList();
        }
    }
}
