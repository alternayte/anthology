namespace Anthology.Modules.Catalog;

public sealed class EmbeddingOptions
{
    public const string Section = "Embedding";
    public const string DefaultBaseUrl = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;
}
