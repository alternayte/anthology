using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class AddTitle
{
    public sealed record AddTitleCommand(int TmdbId, MediaType MediaType = MediaType.Film);

    public sealed record TitleDto(Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType);

    public sealed class Handler(CatalogDbContext db, ITmdbApi tmdb)
    {
        public async Task<Result<TitleDto>> Handle(AddTitleCommand command, CancellationToken ct)
        {
            return command.MediaType switch
            {
                MediaType.TvShow => await AddTvShow(command.TmdbId, ct),
                _ => await AddFilm(command.TmdbId, ct)
            };
        }

        private async Task<Result<TitleDto>> AddFilm(int tmdbId, CancellationToken ct)
        {
            var existing = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ExternalId == tmdbId.ToString(), ct);
            if (existing is not null)
                return new TitleDto(existing.TitleId, existing.Name, existing.Year, existing.PosterPath, existing.MediaType);

            var movie = await tmdb.GetMovieAsync(tmdbId, ct);

            var title = new Title
            {
                TitleId = Guid.NewGuid(),
                ExternalId = movie.Id.ToString(),
                MediaType = MediaType.Film,
                Name = movie.Title,
                Year = SearchTitles.Handler.ParseYear(movie.Release_Date),
                PosterPath = SearchTitles.Handler.PosterUrl(movie.Poster_Path),
                Overview = movie.Overview
            };

            db.Titles.Add(title);
            await db.SaveChangesAsync(ct);

            return new TitleDto(title.TitleId, title.Name, title.Year, title.PosterPath, title.MediaType);
        }

        private async Task<Result<TitleDto>> AddTvShow(int tmdbId, CancellationToken ct)
        {
            var showExternalId = $"tv-{tmdbId}";
            var existing = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ExternalId == showExternalId, ct);
            if (existing is not null)
                return new TitleDto(existing.TitleId, existing.Name, existing.Year, existing.PosterPath, existing.MediaType);

            var show = await tmdb.GetTvShowAsync(tmdbId, ct);

            var showTitle = new Title
            {
                TitleId = Guid.NewGuid(),
                ExternalId = showExternalId,
                MediaType = MediaType.TvShow,
                Name = show.Name,
                Year = SearchTitles.Handler.ParseYear(show.First_Air_Date),
                PosterPath = SearchTitles.Handler.PosterUrl(show.Poster_Path),
                Overview = show.Overview
            };
            showTitle.SetMediaData(new TvShowData(show.Number_Of_Seasons, show.Number_Of_Episodes));
            db.Titles.Add(showTitle);

            for (var s = 1; s <= show.Number_Of_Seasons; s++)
            {
                var season = await tmdb.GetSeasonAsync(tmdbId, s, ct);

                var seasonTitle = new Title
                {
                    TitleId = Guid.NewGuid(),
                    ParentTitleId = showTitle.TitleId,
                    ExternalId = $"tv-{tmdbId}-s{s}",
                    MediaType = MediaType.Season,
                    Name = $"Season {s}",
                    Year = SearchTitles.Handler.ParseYear(season.Air_Date),
                    PosterPath = SearchTitles.Handler.PosterUrl(season.Poster_Path),
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
                        ExternalId = $"tv-{tmdbId}-s{s}e{ep.Episode_Number}",
                        MediaType = MediaType.Episode,
                        Name = string.IsNullOrWhiteSpace(ep.Name) ? $"Episode {ep.Episode_Number}" : ep.Name,
                        SortOrder = ep.Episode_Number
                    };
                    episodeTitle.SetMediaData(new EpisodeData(ep.Season_Number, ep.Episode_Number, ep.Air_Date, ep.Still_Path));
                    db.Titles.Add(episodeTitle);
                }
            }

            await db.SaveChangesAsync(ct);

            return new TitleDto(showTitle.TitleId, showTitle.Name, showTitle.Year, showTitle.PosterPath, showTitle.MediaType);
        }
    }
}
