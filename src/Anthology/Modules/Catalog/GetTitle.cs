using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public static class GetTitle
{
    public sealed record TitleDetailDto(
        Guid TitleId, string Name, int? Year, string? PosterPath, string? Overview, MediaType MediaType);

    public sealed class Handler(CatalogDbContext db)
    {
        public async Task<Result<TitleDetailDto>> Handle(Guid titleId, CancellationToken ct)
        {
            var title = await db.Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == titleId, ct);

            if (title is null)
                return Error.NotFound("catalog.title_not_found", $"Title {titleId} not found.");

            return new TitleDetailDto(
                title.TitleId, title.Name, title.Year, title.PosterPath, title.Overview, title.MediaType);
        }
    }
}
