using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class InboxRow
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = default!;
    public DateTimeOffset ProcessedAt { get; set; }
}

internal sealed class InboxRowConfiguration : IEntityTypeConfiguration<InboxRow>
{
    public void Configure(EntityTypeBuilder<InboxRow> builder)
    {
        builder.ToTable("inbox", "es");
        builder.HasKey(e => new { e.MessageId, e.Consumer });
        builder.Property(e => e.ProcessedAt).HasDefaultValueSql("now()");
    }
}
