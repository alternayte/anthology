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
}

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
    }
}
