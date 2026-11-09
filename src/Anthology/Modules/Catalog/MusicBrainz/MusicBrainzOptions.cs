namespace Anthology.Modules.Catalog;

public sealed class MusicBrainzOptions
{
    public const string Section = "MusicBrainz";
    public const string DefaultBaseUrl = "https://musicbrainz.org/ws/2";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public static Uri ResolveBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        return new Uri(value, UriKind.Absolute);
    }
}
