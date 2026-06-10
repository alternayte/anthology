namespace Anthology.Modules.Catalog;

public sealed class EmbeddingOptions
{
    public const string Section = "Embedding";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;
}
