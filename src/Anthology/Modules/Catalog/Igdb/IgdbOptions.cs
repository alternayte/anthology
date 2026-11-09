namespace Anthology.Modules.Catalog;

public sealed class IgdbOptions
{
    public const string Section = "Igdb";
    public const string DefaultBaseUrl = "https://api.igdb.com/v4/";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
