using System.Text.Json;
using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Catalog;

public sealed class Title
{
    public Guid TitleId { get; set; }
    public string ExternalId { get; set; } = default!;
    public MediaType MediaType { get; set; }
    public string Name { get; set; } = default!;
    public int? Year { get; set; }
    public string? PosterPath { get; set; }
    public string? Overview { get; set; }
    public Guid? ParentTitleId { get; set; }
    public string? MediaData { get; set; }
    public int? SortOrder { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public T? GetMediaData<T>() where T : class =>
        MediaData is null ? null : JsonSerializer.Deserialize<T>(MediaData, JsonOptions);

    public void SetMediaData<T>(T data) where T : class =>
        MediaData = JsonSerializer.Serialize(data, JsonOptions);
}

public sealed record TvShowData(int NumberOfSeasons, int NumberOfEpisodes);
public sealed record SeasonData(int SeasonNumber, int EpisodeCount, string? AirDate);
public sealed record EpisodeData(int SeasonNumber, int EpisodeNumber, string? AirDate, string? StillPath);
public sealed record BookData(string? Author, int? PageCount, string? Isbn);
public sealed record GameData(string? Developer, string? Publisher, string[]? Platforms);
public sealed record MusicData(string? Artist, string? Label, int? TrackCount, string? ReleaseType);

internal sealed class TitleConfiguration : IEntityTypeConfiguration<Title>
{
    public void Configure(EntityTypeBuilder<Title> builder)
    {
        builder.ToTable("titles", "catalog");
        builder.HasKey(t => t.TitleId);
        builder.HasIndex(t => t.ExternalId).IsUnique();
        builder.Property(t => t.ExternalId).IsRequired();
        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.MediaType).HasConversion(new SnakeCaseEnumConverter<MediaType>());
        builder.Property(t => t.MediaData).HasColumnType("jsonb");
        builder.HasOne<Title>().WithMany().HasForeignKey(t => t.ParentTitleId);
        builder.HasIndex(t => new { t.ParentTitleId, t.SortOrder });
    }
}
