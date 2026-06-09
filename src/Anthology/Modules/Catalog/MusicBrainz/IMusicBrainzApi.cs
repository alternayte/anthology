using Refit;

namespace Anthology.Modules.Catalog;

[Headers("Accept: application/json")]
public interface IMusicBrainzApi
{
    [Get("/release-group")]
    Task<MusicBrainzSearchResponse> SearchReleaseGroupsAsync(
        [AliasAs("query")] string query,
        [AliasAs("fmt")] string fmt = "json",
        [AliasAs("limit")] int limit = 20,
        CancellationToken ct = default);

    [Get("/release-group/{id}")]
    Task<MusicBrainzReleaseGroup> GetReleaseGroupAsync(
        string id,
        [AliasAs("inc")] string inc = "artist-credits",
        [AliasAs("fmt")] string fmt = "json",
        CancellationToken ct = default);
}
