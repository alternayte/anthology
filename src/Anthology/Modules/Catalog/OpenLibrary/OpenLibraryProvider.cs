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

    public async Task<Title?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var workId = externalId.Replace("ol-", "");
        var work = await api.GetWorkAsync(workId, ct);

        var searchResponse = await api.SearchAsync(work.Title, 1, ct);
        var doc = searchResponse.Docs.FirstOrDefault(d => d.Key == $"/works/{workId}");

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Book,
            Name = work.Title,
            Year = doc?.First_Publish_Year,
            PosterPath = CoverUrl(work.Covers?.FirstOrDefault()),
            Overview = work.GetDescriptionText()
        };
        title.SetMediaData(new BookData(
            doc?.Author_Name?.FirstOrDefault(),
            doc?.Number_Of_Pages_Median,
            doc?.Isbn?.FirstOrDefault()));

        return title;
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
