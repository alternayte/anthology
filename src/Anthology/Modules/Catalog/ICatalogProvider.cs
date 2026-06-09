namespace Anthology.Modules.Catalog;

public record CatalogSearchResult(
    string ExternalId,
    MediaType MediaType,
    string Name,
    int? Year,
    string? PosterUrl,
    string? Overview);

public interface ICatalogProvider
{
    IReadOnlySet<MediaType> SupportedTypes { get; }
    bool OwnsExternalId(string externalId);
    Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct);
    Task<Title?> GetDetailsAsync(string externalId, CancellationToken ct);
}
