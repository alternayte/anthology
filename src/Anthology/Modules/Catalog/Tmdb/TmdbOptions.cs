namespace Anthology.Modules.Catalog;

public sealed class TmdbOptions
{
    public const string Section = "Tmdb";
    public const string DefaultImageBaseUrl = "https://image.tmdb.org/t/p";

    public string ApiKey { get; set; } = string.Empty;
    public string ImageBaseUrl { get; set; } = DefaultImageBaseUrl;

    public static string ResolveImageBaseUrl(string? imageBaseUrl) =>
        string.IsNullOrWhiteSpace(imageBaseUrl)
            ? DefaultImageBaseUrl
            : imageBaseUrl.Trim().TrimEnd('/');
}
