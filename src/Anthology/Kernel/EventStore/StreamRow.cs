using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class StreamRow
{
    public Guid StreamId { get; set; }
    public string StreamType { get; set; } = default!;
    public int Version { get; set; }
    public string State { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class StreamRowConfiguration : IEntityTypeConfiguration<StreamRow>
{
    public void Configure(EntityTypeBuilder<StreamRow> builder)
    {
        builder.ToTable("streams", "es");
        builder.HasKey(s => s.StreamId);
        builder.Property(s => s.StreamType).IsRequired();
        builder.Property(s => s.State).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
    }
}
