using System.Runtime.CompilerServices;

namespace Anthology.Modules.Catalog;

public sealed class OpenLibraryProvider(IOpenLibraryApi api) : ICatalogProvider, ISeedableProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Book }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public string ProviderName => "openlibrary";
    public IReadOnlySet<MediaType> SeedableTypes => Types;

    private static readonly string[] PopularSubjects = ["fiction", "science_fiction", "fantasy", "mystery", "romance"];

    public async IAsyncEnumerable<CatalogSearchResult> DiscoverAsync(
        SeedOptions options, [EnumeratorCancellation] CancellationToken ct)
    {
        var seen = new HashSet<string>();
        var yielded = 0;

        foreach (var list in options.Lists)
        {
            if (yielded >= options.Count) break;

            if (list == "trending")
            {
                await foreach (var result in DiscoverTrendingAsync(options.Count - yielded, seen, ct))
                {
                    if (yielded >= options.Count) break;
                    yielded++;
                    yield return result;
                }
            }
            else if (list == "popular")
            {
                await foreach (var result in DiscoverSubjectsAsync(options.Count - yielded, seen, ct))
                {
                    if (yielded >= options.Count) break;
                    yielded++;
                    yield return result;
                }
            }
        }
    }

    private async IAsyncEnumerable<CatalogSearchResult> DiscoverTrendingAsync(
        int remaining, HashSet<string> seen, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = 0;
        for (var page = 1; yielded < remaining; page++)
        {
            OpenLibraryTrendingResponse response;
            try { response = await api.GetTrendingAsync("weekly", 20, page, ct); }
            catch { break; }

            if (response.Works.Count == 0) break;

            foreach (var work in response.Works)
            {
                if (yielded >= remaining) break;
                var externalId = $"ol-{work.Key.Replace("/works/", "")}";
                if (seen.Add(externalId))
                {
                    yielded++;
                    yield return new CatalogSearchResult(externalId, MediaType.Book,
                        work.Title, work.First_Publish_Year, CoverUrl(work.Cover_I), null);
                }
            }
        }
    }

    private async IAsyncEnumerable<CatalogSearchResult> DiscoverSubjectsAsync(
        int remaining, HashSet<string> seen, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = 0;
        var perSubject = Math.Max(remaining / PopularSubjects.Length, 20);

        foreach (var subject in PopularSubjects)
        {
            if (yielded >= remaining) break;

            for (var offset = 0; yielded < remaining; offset += 20)
            {
                var fetched = 0;
                OpenLibrarySubjectResponse response;
                try { response = await api.GetSubjectAsync(subject, 20, offset, ct); }
                catch { break; }

                if (response.Works.Count == 0) break;

                foreach (var work in response.Works)
                {
                    if (yielded >= remaining) break;
                    var workId = work.Key.Replace("/works/", "");
                    var externalId = $"ol-{workId}";
                    if (seen.Add(externalId))
                    {
                        yielded++;
                        fetched++;
                        yield return new CatalogSearchResult(externalId, MediaType.Book,
                            work.Title, work.First_Publish_Year, CoverUrl(work.Cover_Id), null);
                    }
                }

                if (fetched >= perSubject) break;
            }
        }
    }

    public bool OwnsExternalId(string externalId) => externalId.StartsWith("ol-");

    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var response = await api.SearchAsync(term, 20, ct);
        return response.Docs.Select(MapSearchResult).ToList();
    }

    public async Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var workId = externalId.Replace("ol-", "");
        var work = await api.GetWorkAsync(workId, ct);

        var searchResponse = await api.SearchAsync(work.Title, 1, ct);
        var doc = searchResponse.Docs.FirstOrDefault(d => d.Key == $"/works/{workId}");

        var subjects = doc?.Subject ?? [];
        var genres = subjects.Take(5).ToArray();
        var keywords = subjects.Skip(5).Take(15).ToArray();

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Book,
            Name = work.Title,
            Year = doc?.First_Publish_Year,
            PosterPath = CoverUrl(work.Covers?.FirstOrDefault()),
            Overview = work.GetDescriptionText(),
            Genres = genres,
            Keywords = keywords
        };
        title.SetMediaData(new BookData(
            doc?.Author_Name?.FirstOrDefault(),
            doc?.Number_Of_Pages_Median,
            doc?.Isbn?.FirstOrDefault()));

        var credits = BuildAuthorCredits(title.TitleId, doc?.Author_Name);
        return new TitleWithCredits(title, credits);
    }

    private static List<TitleCredit> BuildAuthorCredits(Guid titleId, List<string>? authors)
    {
        if (authors is null or []) return [];

        return authors.Select((name, i) => new TitleCredit
        {
            TitleId = titleId,
            ExternalPersonId = $"ol-author-{name.ToLowerInvariant().Replace(' ', '-')}",
            Name = name,
            Role = "author",
            DisplayOrder = i
        }).ToList();
    }

    public static CatalogSearchResult MapSearchResult(OpenLibraryDoc doc) => new(
        $"ol-{doc.Key.Replace("/works/", "")}",
        MediaType.Book,
        doc.Title,
        doc.First_Publish_Year,
        CoverUrl(doc.Cover_I),
        null);

    private static string? CoverUrl(long? coverId) =>
        coverId is not null ? $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg" : null;
}
