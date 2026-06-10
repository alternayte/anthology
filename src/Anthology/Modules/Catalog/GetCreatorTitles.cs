using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class GetCreatorTitles
{
    public sealed record CreatorTitleDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType,
        string SharedPerson, string SharedRole);

    private sealed record RawRow(
        Guid TitleId, string Name, int? Year, string? PosterPath, string MediaType,
        string SharedPerson, string SharedRole);

    private static readonly Dictionary<string, MediaType> MediaTypeMap =
        Enum.GetValues<MediaType>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    public sealed class Handler(CatalogDbContext db)
    {
        public async Task<IReadOnlyList<CreatorTitleDto>> Handle(Guid titleId, CancellationToken ct)
        {
            var source = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (source is null)
                return [];

            var mediaTypeStr = JsonNamingPolicy.SnakeCaseLower.ConvertName(source.MediaType.ToString());

            var sql = @"
                WITH ranked AS (
                    SELECT t.title_id, t.name, t.year, t.poster_path, t.media_type,
                           tc2.name AS shared_person, tc2.role AS shared_role,
                           t.popularity,
                           ROW_NUMBER() OVER (
                               PARTITION BY t.title_id
                               ORDER BY CASE tc2.role WHEN 'director' THEN 1 WHEN 'writer' THEN 2 WHEN 'author' THEN 2 ELSE 3 END
                           ) AS rn
                    FROM catalog.title_credits tc1
                    JOIN catalog.title_credits tc2 ON tc1.external_person_id = tc2.external_person_id
                    JOIN catalog.titles t ON tc2.title_id = t.title_id
                    WHERE tc1.title_id = {0}
                      AND tc2.title_id != {0}
                      AND t.media_type = {1}
                )
                SELECT title_id, name, year, poster_path, media_type, shared_person, shared_role
                FROM ranked
                WHERE rn = 1
                ORDER BY CASE shared_role WHEN 'director' THEN 1 WHEN 'writer' THEN 2 WHEN 'author' THEN 2 ELSE 3 END,
                         popularity DESC NULLS LAST
                LIMIT 12";

            var rows = await db.Database
                .SqlQueryRaw<RawRow>(sql, titleId, mediaTypeStr)
                .ToListAsync(ct);

            return rows.Select(r => new CreatorTitleDto(
                r.TitleId, r.Name, r.Year, r.PosterPath,
                MediaTypeMap.GetValueOrDefault(r.MediaType, Catalog.MediaType.Film),
                r.SharedPerson, r.SharedRole)).ToList();
        }
    }
}
