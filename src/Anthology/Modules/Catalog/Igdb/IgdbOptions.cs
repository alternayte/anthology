namespace Anthology.Modules.Catalog;

public sealed class IgdbOptions
{
    public const string Section = "Igdb";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
