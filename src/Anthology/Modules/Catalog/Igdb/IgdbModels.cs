using System.Text.Json.Serialization;

namespace Anthology.Modules.Catalog;

public sealed class IgdbGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("first_release_date")]
    public long? First_Release_Date { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCover? Cover { get; set; }

    [JsonPropertyName("involved_companies")]
    public List<IgdbInvolvedCompany>? Involved_Companies { get; set; }

    [JsonPropertyName("platforms")]
    public List<IgdbPlatform>? Platforms { get; set; }
}

public sealed class IgdbCover
{
    [JsonPropertyName("image_id")]
    public string? Image_Id { get; set; }
}

public sealed class IgdbInvolvedCompany
{
    [JsonPropertyName("developer")]
    public bool Developer { get; set; }

    [JsonPropertyName("publisher")]
    public bool Publisher { get; set; }

    [JsonPropertyName("company")]
    public IgdbCompany Company { get; set; } = default!;
}

public sealed class IgdbCompany
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}

public sealed class IgdbPlatform
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
