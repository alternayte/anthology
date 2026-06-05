using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class AddTitle
{
    public sealed record AddTitleCommand(int TmdbId);

    public sealed record TitleDto(Guid TitleId, string Name, int? Year, string? PosterPath, MediaType MediaType);

    public sealed class Handler(CatalogDbContext db, TmdbClient tmdb)
    {
        public async Task<Result<TitleDto>> Handle(AddTitleCommand command, CancellationToken ct)
        {
            var existing = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ExternalId == command.TmdbId.ToString(), ct);
            if (existing is not null)
                return new TitleDto(existing.TitleId, existing.Name, existing.Year, existing.PosterPath, existing.MediaType);

            var movie = await tmdb.GetMovieAsync(command.TmdbId, ct);
            if (movie is null)
                return Error.NotFound("catalog.tmdb_not_found", $"TMDB movie {command.TmdbId} not found.");

            var title = new Title
            {
                TitleId = Guid.NewGuid(),
                ExternalId = movie.Id.ToString(),
                MediaType = MediaType.Film,
                Name = movie.Title,
                Year = DateTime.TryParse(movie.Release_Date, out var d) ? d.Year : null,
                PosterPath = movie.Poster_Path is not null ? $"https://image.tmdb.org/t/p/w342{movie.Poster_Path}" : null,
                Overview = movie.Overview
            };

            db.Titles.Add(title);
            await db.SaveChangesAsync(ct);

            return new TitleDto(title.TitleId, title.Name, title.Year, title.PosterPath, title.MediaType);
        }
    }
}
