namespace Anthology.Modules.Catalog;

public sealed class IgdbOptions
{
    public const string Section = "Igdb";
    public const string DefaultImageBaseUrl = "https://images.igdb.com/igdb/image/upload";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ImageBaseUrl { get; set; } = DefaultImageBaseUrl;
}
