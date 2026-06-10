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

    [Get("/trending/{period}.json")]
    Task<OpenLibraryTrendingResponse> GetTrendingAsync(
        string period,
        [AliasAs("limit")] int limit = 20,
        [AliasAs("page")] int page = 1,
        CancellationToken ct = default);

    [Get("/subjects/{subject}.json")]
    Task<OpenLibrarySubjectResponse> GetSubjectAsync(
        string subject,
        [AliasAs("limit")] int limit = 20,
        [AliasAs("offset")] int offset = 0,
        CancellationToken ct = default);
}
