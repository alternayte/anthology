namespace Anthology.Modules.Catalog;

public interface ISeedableProvider
{
    string ProviderName { get; }
    IAsyncEnumerable<CatalogSearchResult> DiscoverAsync(SeedOptions options, CancellationToken ct);
}

public record SeedOptions(
    int Count = 500,
    string[] Lists = default!)
{
    public string[] Lists { get; init; } = Lists ?? ["popular", "top_rated", "trending"];
}

public record SeedCommandOptions(
    int Count = 500,
    string[]? Providers = null,
    string[] Lists = default!,
    MediaType[]? MediaTypes = null)
{
    public string[] Lists { get; init; } = Lists ?? ["popular", "top_rated", "trending"];
}
