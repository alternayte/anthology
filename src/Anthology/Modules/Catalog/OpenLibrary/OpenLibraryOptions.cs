namespace Anthology.Modules.Catalog;

public sealed class OpenLibraryOptions
{
    public const string Section = "OpenLibrary";
    public const string DefaultBaseUrl = "https://openlibrary.org";
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
