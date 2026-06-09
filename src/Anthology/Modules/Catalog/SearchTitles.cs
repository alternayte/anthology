using Microsoft.Extensions.Logging;

namespace Anthology.Modules.Catalog;

public static class SearchTitles
{
    public sealed record Query(string Term, MediaType? MediaType);

    private static readonly MediaType[] TypeOrder =
        [MediaType.Film, MediaType.TvShow, MediaType.Book, MediaType.Game, MediaType.Music];

    public sealed class Handler(IEnumerable<ICatalogProvider> providers, ILogger<Handler>? logger = null)
    {
        private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(3);

        public async Task<IReadOnlyList<CatalogSearchResult>> Handle(Query query, CancellationToken ct)
        {
            var matching = providers
                .Where(p => query.MediaType is null || p.SupportedTypes.Contains(query.MediaType.Value))
                .ToList();

            var tasks = matching.Select(p => SearchWithTimeout(p, query.Term, ct));
            var resultSets = await Task.WhenAll(tasks);

            return resultSets
                .SelectMany(r => r)
                .OrderBy(r => Array.IndexOf(TypeOrder, r.MediaType))
                .ToList();
        }

        private async Task<IReadOnlyList<CatalogSearchResult>> SearchWithTimeout(
            ICatalogProvider provider, string term, CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ProviderTimeout);
                return await provider.SearchAsync(term, cts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger?.LogWarning(ex, "Provider {Provider} failed for term '{Term}'",
                    provider.GetType().Name, term);
                return [];
            }
        }
    }
}
