using Microsoft.Extensions.Options;

namespace Anthology.Modules.Catalog;

public sealed class OpenLibraryOptions : IOptions<OpenLibraryOptions>
{
    public const string Section = "OpenLibrary";
    public const string DefaultCoverBaseUrl = "https://covers.openlibrary.org/b/id";

    public string CoverBaseUrl { get; set; } = DefaultCoverBaseUrl;

    public OpenLibraryOptions Value => this;

    public static string ResolveCoverBaseUrl(string? coverBaseUrl) =>
        string.IsNullOrWhiteSpace(coverBaseUrl)
            ? DefaultCoverBaseUrl
            : coverBaseUrl.TrimEnd('/');
}
