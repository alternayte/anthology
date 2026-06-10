using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class AddTitle
{
    public sealed record Command(string ExternalId);

    public sealed record TitleDto(Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType);

    public sealed class Handler(CatalogDbContext db, IEnumerable<ICatalogProvider> providers, ITmdbApi tmdb)
    {
        public async Task<Result<TitleDto>> Handle(Command command, CancellationToken ct)
        {
            var existing = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ExternalId == command.ExternalId, ct);
            if (existing is not null)
                return new TitleDto(existing.TitleId, existing.Name, existing.Year, existing.PosterPath, existing.MediaType);

            var provider = providers.FirstOrDefault(p => p.OwnsExternalId(command.ExternalId));
            if (provider is null)
                return Error.Validation("catalog.unknown_provider", $"No provider found for external ID '{command.ExternalId}'.");

            var result = await provider.GetDetailsAsync(command.ExternalId, ct);
            if (result is null)
                return Error.NotFound("catalog.title_not_found", $"Title '{command.ExternalId}' not found in external catalog.");

            var title = result.Title;
            db.Titles.Add(title);

            if (result.Credits.Count > 0)
                db.TitleCredits.AddRange(result.Credits);

            if (title.MediaType == MediaType.TvShow && command.ExternalId.StartsWith("tmdb-tv-"))
                await AddTvShowChildren(title, command.ExternalId, ct);

            await db.SaveChangesAsync(ct);
            return new TitleDto(title.TitleId, title.Name, title.Year, title.PosterPath, title.MediaType);
        }

        private async Task AddTvShowChildren(Title showTitle, string externalId, CancellationToken ct)
        {
            var tmdbId = int.Parse(externalId.Replace("tmdb-tv-", ""));
            var show = await tmdb.GetTvShowAsync(tmdbId, ct);

            for (var s = 1; s <= show.Number_Of_Seasons; s++)
            {
                var season = await tmdb.GetSeasonAsync(tmdbId, s, ct);

                var seasonTitle = new Title
                {
                    TitleId = Guid.NewGuid(),
                    ParentTitleId = showTitle.TitleId,
                    ExternalId = $"tmdb-tv-{tmdbId}-s{s}",
                    MediaType = MediaType.Season,
                    Name = $"Season {s}",
                    Year = TmdbProvider.ParseYear(season.Air_Date),
                    PosterPath = TmdbProvider.PosterUrl(season.Poster_Path),
                    SortOrder = s
                };
                seasonTitle.SetMediaData(new SeasonData(season.Season_Number, season.Episodes.Count, season.Air_Date));
                db.Titles.Add(seasonTitle);

                foreach (var ep in season.Episodes)
                {
                    var episodeTitle = new Title
                    {
                        TitleId = Guid.NewGuid(),
                        ParentTitleId = seasonTitle.TitleId,
                        ExternalId = $"tmdb-tv-{tmdbId}-s{s}e{ep.Episode_Number}",
                        MediaType = MediaType.Episode,
                        Name = string.IsNullOrWhiteSpace(ep.Name) ? $"Episode {ep.Episode_Number}" : ep.Name,
                        SortOrder = ep.Episode_Number
                    };
                    episodeTitle.SetMediaData(new EpisodeData(ep.Season_Number, ep.Episode_Number, ep.Air_Date, ep.Still_Path));
                    db.Titles.Add(episodeTitle);
                }
            }
        }
    }
}
