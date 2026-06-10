using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;

namespace Anthology.Workers;

public sealed class EmbeddingWorker(
    IServiceScopeFactory scopeFactory,
    HttpClient httpClient,
    IOptions<EmbeddingOptions> options,
    ILogger<EmbeddingWorker> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            log.LogWarning("Embedding API key not configured — embedding worker disabled");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(ct);
                if (processed == 0)
                    await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled error in embedding worker, retrying in 30s");
                await Task.Delay(PollInterval, ct);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var titles = await db.Titles
            .AsNoTracking()
            .Where(t => t.Embedding == null
                        && t.MediaType != MediaType.Season
                        && t.MediaType != MediaType.Episode)
            .OrderBy(t => t.TitleId)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (titles.Count == 0) return 0;

        foreach (var title in titles)
        {
            var text = BuildEmbeddingText(title.Name, title.Genres, title.Keywords, title.Overview);
            var embedding = await GetEmbeddingWithRetryAsync(text, ct);

            if (embedding is null)
            {
                log.LogWarning("Failed to get embedding for title {TitleId} after retries, skipping", title.TitleId);
                continue;
            }

            await db.Titles
                .Where(t => t.TitleId == title.TitleId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Embedding, new Vector(embedding))
                    .SetProperty(t => t.EmbeddingModel, options.Value.Model), ct);

            log.LogDebug("Embedded title {TitleId} ({Name})", title.TitleId, title.Name);
        }

        return titles.Count;
    }

    private async Task<float[]?> GetEmbeddingWithRetryAsync(string text, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                return await CallEmbeddingApiAsync(text, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= RetryDelays.Length)
                {
                    log.LogError(ex, "Embedding API call failed after {Attempts} retries", RetryDelays.Length);
                    return null;
                }

                var delay = RetryDelays[attempt];
                log.LogWarning(ex, "Embedding API call failed, retrying in {Delay}", delay);
                await Task.Delay(delay, ct);
            }
        }

        return null;
    }

    private async Task<float[]> CallEmbeddingApiAsync(string text, CancellationToken ct)
    {
        var request = new
        {
            input = text,
            model = options.Value.Model,
            dimensions = options.Value.Dimensions
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("https://api.openai.com/v1/embeddings", content, ct);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var embeddingArray = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        var floats = new float[embeddingArray.GetArrayLength()];
        var i = 0;
        foreach (var element in embeddingArray.EnumerateArray())
        {
            floats[i++] = element.GetSingle();
        }

        return floats;
    }

    internal static string BuildEmbeddingText(
        string name,
        string[]? genres,
        string[]? keywords,
        string? overview)
    {
        var sb = new StringBuilder(name);

        if (genres is { Length: > 0 })
            sb.Append(". ").Append(string.Join(", ", genres));

        if (keywords is { Length: > 0 })
            sb.Append(". ").Append(string.Join(", ", keywords));

        if (!string.IsNullOrWhiteSpace(overview))
            sb.Append(". ").Append(overview);

        return sb.ToString();
    }
}
