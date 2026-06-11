using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class GetSimilar
{
    public sealed record SimilarTitleDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType, string[]? Genres);

    private static readonly Dictionary<string, MediaType> MediaTypeMap =
        Enum.GetValues<MediaType>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    public sealed class Handler(CatalogDbContext db, FindSimilarTitles similar)
    {
        public async Task<IReadOnlyList<SimilarTitleDto>> Handle(Guid titleId, CancellationToken ct)
        {
            var source = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (source is null)
                return [];

            var mediaTypeStr = JsonNamingPolicy.SnakeCaseLower.ConvertName(source.MediaType.ToString());

            var rows = source.Embedding is not null
                ? await similar.ByEmbedding(source.TitleId, source.Embedding, mediaTypeStr, [], 12, ct)
                : await similar.ByOverlap(source.TitleId, source.Genres ?? [], source.Keywords ?? [], mediaTypeStr, [], 12, ct);

            return rows.Select(r => new SimilarTitleDto(
                r.TitleId, r.Name, r.Year, r.PosterPath,
                MediaTypeMap.GetValueOrDefault(r.MediaType, MediaType.Film),
                r.Genres)).ToList();
        }
    }
}
