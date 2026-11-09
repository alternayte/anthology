namespace Anthology.Modules.Catalog;

public sealed class IgdbOptions
{
    public const string Section = "Igdb";
    public const string DefaultBaseUrl = "https://api.igdb.com/v4/";
    public const string DefaultImageBaseUrl = "https://images.igdb.com/igdb/image/upload";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string ImageBaseUrl { get; set; } = DefaultImageBaseUrl;
}
