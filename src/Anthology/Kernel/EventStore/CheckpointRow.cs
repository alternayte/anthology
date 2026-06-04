using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class CheckpointRow
{
    public string ProjectionName { get; set; } = default!;
    public long Position { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class CheckpointRowConfiguration : IEntityTypeConfiguration<CheckpointRow>
{
    public void Configure(EntityTypeBuilder<CheckpointRow> builder)
    {
        builder.ToTable("checkpoints", "es");
        builder.HasKey(c => c.ProjectionName);
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
    }
}
