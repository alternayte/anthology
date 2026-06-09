namespace Anthology.Modules.Catalog;

public sealed class MusicBrainzProvider(IMusicBrainzApi? api) : ICatalogProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Music }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public bool OwnsExternalId(string externalId) => externalId.StartsWith("mb-");

    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var response = await api!.SearchReleaseGroupsAsync(term, ct: ct);
        return response.Release_Groups.Select(MapSearchResult).ToList();
    }

    public async Task<Title?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var mbid = externalId.Replace("mb-", "");
        var rg = await api!.GetReleaseGroupAsync(mbid, ct: ct);

        var artist = rg.Artist_Credit?.FirstOrDefault()?.Name;
        var releaseType = rg.Primary_Type?.ToLowerInvariant();

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Music,
            Name = rg.Title,
            Year = ParseYear(rg.First_Release_Date),
            PosterPath = CoverUrl(rg.Id),
            Overview = FormatOverview(artist, rg.Primary_Type)
        };
        title.SetMediaData(new MusicData(artist, null, null, releaseType));
        return title;
    }

    public static CatalogSearchResult MapSearchResult(MusicBrainzReleaseGroup rg) => new(
        $"mb-{rg.Id}",
        MediaType.Music,
        rg.Title,
        ParseYear(rg.First_Release_Date),
        CoverUrl(rg.Id),
        FormatOverview(rg.Artist_Credit?.FirstOrDefault()?.Name, rg.Primary_Type));

    private static int? ParseYear(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date[..4], out var y) ? y : null;

    private static string? CoverUrl(string mbid) =>
        $"https://coverartarchive.org/release-group/{mbid}/front-250";

    private static string? FormatOverview(string? artist, string? type) =>
        (artist, type) switch
        {
            (not null, not null) => $"{artist} — {type}",
            (not null, _) => artist,
            (_, not null) => type,
            _ => null
        };
}
