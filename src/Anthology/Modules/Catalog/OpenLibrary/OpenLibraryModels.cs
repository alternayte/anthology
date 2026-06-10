using System.Text.Json.Serialization;

namespace Anthology.Modules.Catalog;

public sealed record OpenLibrarySearchResponse(
    [property: JsonPropertyName("docs")] List<OpenLibraryDoc> Docs);

public sealed class OpenLibraryDoc
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("first_publish_year")]
    public int? First_Publish_Year { get; set; }

    [JsonPropertyName("author_name")]
    public List<string>? Author_Name { get; set; }

    [JsonPropertyName("cover_i")]
    public long? Cover_I { get; set; }

    [JsonPropertyName("number_of_pages_median")]
    public int? Number_Of_Pages_Median { get; set; }

    [JsonPropertyName("isbn")]
    public List<string>? Isbn { get; set; }

    [JsonPropertyName("subject")]
    public List<string>? Subject { get; set; }
}

public sealed class OpenLibraryWork
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("description")]
    public object? Description { get; set; }

    [JsonPropertyName("covers")]
    public List<long>? Covers { get; set; }

    public string? GetDescriptionText() => Description switch
    {
        string s => s,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } el => el.GetString(),
        System.Text.Json.JsonElement el when el.TryGetProperty("value", out var v) => v.GetString(),
        _ => null
    };
}

public sealed class OpenLibraryTrendingResponse
{
    [JsonPropertyName("works")]
    public List<OpenLibraryTrendingWork> Works { get; set; } = [];
}

public sealed class OpenLibraryTrendingWork
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("first_publish_year")]
    public int? First_Publish_Year { get; set; }

    [JsonPropertyName("cover_i")]
    public long? Cover_I { get; set; }

    [JsonPropertyName("author_name")]
    public List<string>? Author_Name { get; set; }
}

public sealed class OpenLibrarySubjectResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("works")]
    public List<OpenLibrarySubjectWork> Works { get; set; } = [];

    [JsonPropertyName("work_count")]
    public int Work_Count { get; set; }
}

public sealed class OpenLibrarySubjectWork
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("cover_id")]
    public long? Cover_Id { get; set; }

    [JsonPropertyName("first_publish_year")]
    public int? First_Publish_Year { get; set; }

    [JsonPropertyName("authors")]
    public List<OpenLibrarySubjectAuthor>? Authors { get; set; }
}

public sealed class OpenLibrarySubjectAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;
}
