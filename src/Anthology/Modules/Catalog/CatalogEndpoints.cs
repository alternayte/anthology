using Anthology.Kernel;

namespace Anthology.Modules.Catalog;

public static class CatalogEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/search", async (string term, SearchTitles.Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new SearchTitles.Query(term), ct)))
            .WithName("searchCatalog").Produces<IReadOnlyList<SearchTitles.TitleSearchResult>>();

        group.MapPost("/titles", async (AddTitle.AddTitleCommand command, AddTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization().WithName("addTitle").Produces<AddTitle.TitleDto>();

        group.MapGet("/titles/{titleId:guid}", async (Guid titleId, GetTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(titleId, ct)).ToHttpResult())
            .WithName("getTitle").Produces<GetTitle.TitleDetailDto>();

        return app;
    }
}
