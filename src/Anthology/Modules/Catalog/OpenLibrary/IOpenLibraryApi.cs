using Refit;

namespace Anthology.Modules.Catalog;

[Headers("Accept: application/json")]
public interface IOpenLibraryApi
{
    [Get("/search.json")]
    Task<OpenLibrarySearchResponse> SearchAsync(
        [AliasAs("q")] string query,
        [AliasAs("limit")] int limit = 20,
        CancellationToken ct = default);

    [Get("/works/{workId}.json")]
    Task<OpenLibraryWork> GetWorkAsync(string workId, CancellationToken ct = default);
}
