using Microsoft.Extensions.Logging;

namespace Anthology.Modules.Catalog;

public sealed class CatalogSeeder(
    IEnumerable<ISeedableProvider> providers,
    AddTitle.Handler addTitle,
    ILogger<CatalogSeeder> logger)
{
    public async Task SeedAsync(SeedCommandOptions options, CancellationToken ct)
    {
        var totalProcessed = 0;

        var activeProviders = providers
            .Where(p => options.Providers is null ||
                        options.Providers.Any(name =>
                            string.Equals(name, p.ProviderName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (activeProviders.Count == 0)
        {
            logger.LogWarning("No matching seedable providers found");
            return;
        }

        foreach (var provider in activeProviders)
        {
            var seedOptions = new SeedOptions(options.Count, options.Lists);
            var seen = new HashSet<string>();
            var processed = 0;

            Console.WriteLine("[{0}] Starting seed (count={1}, lists=[{2}])",
                provider.ProviderName, options.Count, string.Join(", ", options.Lists));

            await foreach (var result in provider.DiscoverAsync(seedOptions, ct))
            {
                if (processed >= options.Count) break;
                if (!seen.Add(result.ExternalId)) continue;
                if (options.MediaTypes is not null && !options.MediaTypes.Contains(result.MediaType))
                    continue;

                var addResult = await addTitle.Handle(new AddTitle.Command(result.ExternalId), ct);

                if (addResult.IsError)
                {
                    logger.LogWarning("[{Provider}] Skipped {ExternalId}: {Error}",
                        provider.ProviderName, result.ExternalId, addResult.Error.Message);
                    continue;
                }

                processed++;
                Console.WriteLine("[{0}] {1}/{2} - {3} ({4})",
                    provider.ProviderName, processed, options.Count,
                    addResult.Value.Name, addResult.Value.Year);
            }

            totalProcessed += processed;
            Console.WriteLine("[{0}] Done: {1} titles processed", provider.ProviderName, processed);
        }

        Console.WriteLine();
        Console.WriteLine("Seed complete: {0} titles processed. Embeddings will generate in background.",
            totalProcessed);
    }
}
