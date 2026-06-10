using System.Runtime.CompilerServices;

namespace Anthology.Modules.Catalog;

public sealed class IgdbProvider(IgdbClient? client) : ICatalogProvider, ISeedableProvider
{
    private static readonly IReadOnlySet<MediaType> Types =
        new HashSet<MediaType> { MediaType.Game }.AsReadOnly();

    public IReadOnlySet<MediaType> SupportedTypes => Types;

    public string ProviderName => "igdb";

    private static readonly string Fields =
        "name,first_release_date,summary,cover.image_id,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,platforms.name,genres.name,themes.name,keywords.name,total_rating,total_rating_count";

    public async IAsyncEnumerable<CatalogSearchResult> DiscoverAsync(
        SeedOptions options, [EnumeratorCancellation] CancellationToken ct)
    {
        var seen = new HashSet<string>();
        var yielded = 0;

        foreach (var list in options.Lists)
        {
            if (yielded >= options.Count) break;

            var (sort, where) = list switch
            {
                "popular" => ("total_rating_count desc", "where total_rating_count > 0;"),
                "top_rated" => ("total_rating desc", "where total_rating_count > 50;"),
                "trending" => ("first_release_date desc", "where total_rating_count > 10;"),
                _ => (null, null)
            };

            if (sort is null) continue;

            for (var offset = 0; yielded < options.Count; offset += 50)
            {
                var body = $"fields {Fields}; sort {sort}; {where} offset {offset}; limit 50;";
                var games = await client!.DiscoverGamesAsync(body, ct);
                if (games.Count == 0) break;

                foreach (var game in games)
                {
                    if (yielded >= options.Count) break;
                    var result = MapSearchResult(game);
                    if (seen.Add(result.ExternalId))
                    {
                        yielded++;
                        yield return result;
                    }
                }
            }
        }
    }

    public bool OwnsExternalId(string externalId) => externalId.StartsWith("igdb-");

    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var games = await client!.SearchGamesAsync(term, ct);
        return games.Select(MapSearchResult).ToList();
    }

    public async Task<TitleWithCredits?> GetDetailsAsync(string externalId, CancellationToken ct)
    {
        var igdbId = int.Parse(externalId.Replace("igdb-", ""));
        var game = await client!.GetGameAsync(igdbId, ct);
        if (game is null) return null;

        var developer = game.Involved_Companies?
            .FirstOrDefault(c => c.Developer)?.Company.Name;
        var publisher = game.Involved_Companies?
            .FirstOrDefault(c => c.Publisher)?.Company.Name;
        var platforms = game.Platforms?.Select(p => p.Name).ToArray();

        var genres = (game.Genres?.Select(g => g.Name) ?? [])
            .Concat(game.Themes?.Select(t => t.Name) ?? [])
            .Distinct()
            .ToArray();
        var keywords = game.Keywords?.Select(k => k.Name).ToArray() ?? [];

        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = externalId,
            MediaType = MediaType.Game,
            Name = game.Name,
            Year = EpochToYear(game.First_Release_Date),
            PosterPath = CoverUrl(game.Cover?.Image_Id),
            Overview = game.Summary,
            Genres = genres,
            Keywords = keywords,
            VoteAverage = game.Total_Rating is not null ? game.Total_Rating.Value / 10.0 : null
        };
        title.SetMediaData(new GameData(developer, publisher, platforms));

        var credits = BuildCompanyCredits(title.TitleId, game.Involved_Companies);
        return new TitleWithCredits(title, credits);
    }

    private static List<TitleCredit> BuildCompanyCredits(Guid titleId, List<IgdbInvolvedCompany>? companies)
    {
        if (companies is null or []) return [];

        var result = new List<TitleCredit>();
        var order = 0;

        foreach (var company in companies.Where(c => c.Developer))
        {
            result.Add(new TitleCredit
            {
                TitleId = titleId,
                ExternalPersonId = $"igdb-company-{company.Company.Name.ToLowerInvariant().Replace(' ', '-')}",
                Name = company.Company.Name,
                Role = "developer",
                DisplayOrder = order++
            });
        }

        foreach (var company in companies.Where(c => c.Publisher))
        {
            result.Add(new TitleCredit
            {
                TitleId = titleId,
                ExternalPersonId = $"igdb-company-{company.Company.Name.ToLowerInvariant().Replace(' ', '-')}",
                Name = company.Company.Name,
                Role = "publisher",
                DisplayOrder = order++
            });
        }

        return result;
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
