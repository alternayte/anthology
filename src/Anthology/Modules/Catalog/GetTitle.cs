using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class GetTitle
{
    public record TitleDetailDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, string? Overview, MediaType MediaType);

    public sealed record TvShowDetailDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, string? Overview,
        TvShowData ShowData, List<SeasonDto> Seasons)
        : TitleDetailDto(TitleId, Name, Year, PosterPath, Overview, MediaType.TvShow);

    public sealed record SeasonDto(Guid TitleId, string Name, int SeasonNumber, List<EpisodeDto> Episodes);

    public sealed record EpisodeDto(Guid TitleId, string Name, int SeasonNumber, int EpisodeNumber, string? AirDate, string? StillPath);

    public sealed class Handler(CatalogDbContext db)
    {
        public async Task<Result<TitleDetailDto>> Handle(Guid titleId, CancellationToken ct)
        {
            var title = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (title is null)
                return Error.NotFound("catalog.title_not_found", $"Title {titleId} not found.");

            if (title.MediaType == MediaType.TvShow)
                return await BuildTvShowDto(title, ct);

            return new TitleDetailDto(
                title.TitleId, title.Name, title.Year, title.PosterPath, title.Overview, title.MediaType);
        }

        private async Task<TvShowDetailDto> BuildTvShowDto(Title show, CancellationToken ct)
        {
            // Load seasons
            var seasonRows = await db.Titles.AsNoTracking()
                .Where(t => t.ParentTitleId == show.TitleId && t.MediaType == MediaType.Season)
                .OrderBy(t => t.SortOrder)
                .ToListAsync(ct);

            var seasonIds = seasonRows.Select(s => s.TitleId).ToList();

            // Load all episodes for all seasons in one query
            var episodeRows = await db.Titles.AsNoTracking()
                .Where(t => t.MediaType == MediaType.Episode && seasonIds.Contains(t.ParentTitleId!.Value))
                .OrderBy(t => t.SortOrder)
                .ToListAsync(ct);

            var episodesBySeasonId = episodeRows
                .GroupBy(e => e.ParentTitleId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var seasons = seasonRows.Select(season =>
            {
                var seasonData = season.GetMediaData<SeasonData>();
                var episodes = episodesBySeasonId.TryGetValue(season.TitleId, out var eps) ? eps : [];

                var episodeDtos = episodes
                    .OrderBy(e => e.SortOrder)
                    .Select(ep =>
                    {
                        var epData = ep.GetMediaData<EpisodeData>();
                        return new EpisodeDto(
                            ep.TitleId,
                            ep.Name,
                            epData?.SeasonNumber ?? 0,
                            epData?.EpisodeNumber ?? ep.SortOrder ?? 0,
                            epData?.AirDate,
                            epData?.StillPath);
                    })
                    .ToList();

                return new SeasonDto(
                    season.TitleId,
                    season.Name,
                    seasonData?.SeasonNumber ?? season.SortOrder ?? 0,
                    episodeDtos);
            }).ToList();

            var showData = show.GetMediaData<TvShowData>() ?? new TvShowData(0, 0);

            return new TvShowDetailDto(
                show.TitleId, show.Name, show.Year, show.PosterPath, show.Overview,
                showData, seasons);
        }
    }
}
