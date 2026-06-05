using Anthology.Kernel;

namespace Anthology.Modules.Catalog;

public static class CatalogEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/search", async (string term, SearchTitles.Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new SearchTitles.Query(term), ct)));

        group.MapPost("/titles", async (AddTitle.Command command, AddTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapGet("/titles/{titleId:guid}", async (Guid titleId, GetTitle.Handler handler, CancellationToken ct) =>
            (await handler.Handle(titleId, ct)).ToHttpResult());

        return app;
    }
}
