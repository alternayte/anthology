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

    public async Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var mbid = externalId.Replace("mb-", "");
        var rg = await api!.GetReleaseGroupAsync(mbid, ct: ct);

        var artist = rg.Artist_Credit?.FirstOrDefault()?.Name;
        var releaseType = rg.Primary_Type?.ToLowerInvariant();

        var sortedTags = (rg.Tags ?? []).OrderByDescending(t => t.Count).Select(t => t.Name).ToList();
        var genres = sortedTags.Take(5).ToArray();
        var keywords = sortedTags.Skip(5).Take(15).ToArray();

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Music,
            Name = rg.Title,
            Year = ParseYear(rg.First_Release_Date),
            PosterPath = CoverUrl(rg.Id),
            Overview = FormatOverview(artist, rg.Primary_Type),
            Genres = genres,
            Keywords = keywords
        };
        title.SetMediaData(new MusicData(artist, null, null, releaseType));

        var credits = BuildArtistCredits(title.TitleId, rg.Artist_Credit);
        return new TitleWithCredits(title, credits);
    }

    private static List<TitleCredit> BuildArtistCredits(Guid titleId, List<MusicBrainzArtistCredit>? artists)
    {
        if (artists is null or []) return [];

        return artists.Select((a, i) => new TitleCredit
        {
            TitleId = titleId,
            ExternalPersonId = $"mb-artist-{a.Name.ToLowerInvariant().Replace(' ', '-')}",
            Name = a.Name,
            Role = "artist",
            DisplayOrder = i
        }).ToList();
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
