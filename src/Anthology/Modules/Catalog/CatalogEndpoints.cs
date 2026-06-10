using Anthology.Kernel;

namespace Anthology.Modules.Catalog;

public static class CatalogEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/search", async (string term, string? mediaType, SearchTitles.Handler handler, CancellationToken ct) =>
        {
            MediaType? media = Enum.TryParse<MediaType>(mediaType, true, out var m) ? m : null;
            return Results.Ok(await handler.Handle(new SearchTitles.Query(term, media), ct));
        })
        .WithName("searchCatalog").Produces<List<CatalogSearchResult>>();

        group.MapGet("/search/local", async (string term, string? mediaType, SearchLocal.Handler handler, CancellationToken ct) =>
        {
            MediaType? media = Enum.TryParse<MediaType>(mediaType, true, out var m) ? m : null;
            return Results.Ok(await handler.Handle(new SearchLocal.Query(term, media, null), ct));
        })
        .WithName("searchLocal").Produces<List<SearchLocal.LocalSearchResult>>();

        group.MapPost("/titles", async (AddTitle.Command command, AddTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization().WithName("addTitle").Produces<AddTitle.TitleDto>();

        group.MapGet("/titles/{titleId:guid}", async (Guid titleId, GetTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(titleId, ct)).ToHttpResult())
            .WithName("getTitle").Produces<GetTitle.TitleDetailDto>();

        return app;
    }
}
