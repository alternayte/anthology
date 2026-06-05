using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class RebuildJobRow
{
    public Guid Id { get; set; }
    public string StreamType { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Failed { get; set; }
    public string Errors { get; set; } = "[]";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class RebuildJobRowConfiguration : IEntityTypeConfiguration<RebuildJobRow>
{
    public void Configure(EntityTypeBuilder<RebuildJobRow> builder)
    {
        builder.ToTable("rebuild_jobs", "es");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.StreamType).IsRequired();
        builder.Property(j => j.Status).IsRequired();
        builder.Property(j => j.Errors).HasColumnType("jsonb").IsRequired();
    }
}
