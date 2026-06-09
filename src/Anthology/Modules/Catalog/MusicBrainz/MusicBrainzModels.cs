using System.Text.Json.Serialization;

namespace Anthology.Modules.Catalog;

public sealed class MusicBrainzSearchResponse
{
    [JsonPropertyName("release-groups")]
    public List<MusicBrainzReleaseGroup> Release_Groups { get; set; } = [];
}

public sealed class MusicBrainzReleaseGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("primary-type")]
    public string? Primary_Type { get; set; }

    [JsonPropertyName("first-release-date")]
    public string? First_Release_Date { get; set; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCredit>? Artist_Credit { get; set; }
}

public sealed class MusicBrainzArtistCredit
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
