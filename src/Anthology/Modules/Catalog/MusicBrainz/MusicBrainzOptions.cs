namespace Anthology.Modules.Catalog;

public sealed class MusicBrainzOptions
{
    public const string Section = "MusicBrainz";
    public const string DefaultCoverBaseUrl = "https://coverartarchive.org/release-group";

    public string CoverBaseUrl { get; set; } = DefaultCoverBaseUrl;
}
