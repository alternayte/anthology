using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class OutboxRow
{
    public Guid Id { get; set; }
    public string AggregateType { get; set; } = default!;
    public string AggregateId { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public string? Traceparent { get; set; }
}

internal sealed class OutboxRowConfiguration : IEntityTypeConfiguration<OutboxRow>
{
    public void Configure(EntityTypeBuilder<OutboxRow> builder)
    {
        builder.ToTable("outbox", "es");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AggregateType).IsRequired();
        builder.Property(e => e.AggregateId).IsRequired();
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
    }
}
