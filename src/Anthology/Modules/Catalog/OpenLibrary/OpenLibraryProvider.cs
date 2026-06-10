namespace Anthology.Modules.Catalog;

public sealed class OpenLibraryProvider(IOpenLibraryApi api) : ICatalogProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Book }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

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
