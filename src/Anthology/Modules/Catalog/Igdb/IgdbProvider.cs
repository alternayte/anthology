namespace Anthology.Modules.Catalog;

public sealed class IgdbProvider(IgdbClient? client) : ICatalogProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Game }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public bool OwnsExternalId(string externalId) => externalId.StartsWith("igdb-");

    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var games = await client!.SearchGamesAsync(term, ct);
        return games.Select(MapSearchResult).ToList();
    }

    public async Task<Title?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var igdbId = int.Parse(externalId.Replace("igdb-", ""));
        var game = await client!.GetGameAsync(igdbId, ct);
        if (game is null) return null;

        var developer = game.Involved_Companies?
            .FirstOrDefault(c => c.Developer)?.Company.Name;
        var publisher = game.Involved_Companies?
            .FirstOrDefault(c => c.Publisher)?.Company.Name;
        var platforms = game.Platforms?.Select(p => p.Name).ToArray();

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Game,
            Name = game.Name,
            Year = EpochToYear(game.First_Release_Date),
            PosterPath = CoverUrl(game.Cover?.Image_Id),
            Overview = game.Summary
        };
        title.SetMediaData(new GameData(developer, publisher, platforms));
        return title;
    }

    public static CatalogSearchResult MapSearchResult(IgdbGame game) => new(
        $"igdb-{game.Id}",
        MediaType.Game,
        game.Name,
        EpochToYear(game.First_Release_Date),
        CoverUrl(game.Cover?.Image_Id),
        game.Summary);

    internal static int? EpochToYear(long? epoch) =>
        epoch is not null ? DateTimeOffset.FromUnixTimeSeconds(epoch.Value).Year : null;

    private static string? CoverUrl(string? imageId) =>
        imageId is not null ? $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg" : null;
}
