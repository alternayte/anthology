using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Kernel.EventStore;

public sealed class EventRow
{
    public long GlobalPosition { get; set; }
    public Guid StreamId { get; set; }
    public int Version { get; set; }
    public string EventType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public string Metadata { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public uint Xid { get; set; }
}

internal sealed class EventRowConfiguration : IEntityTypeConfiguration<EventRow>
{
    public void Configure(EntityTypeBuilder<EventRow> builder)
    {
        builder.ToTable("events", "es");
        builder.HasKey(e => new { e.StreamId, e.Version });
        builder.Property(e => e.GlobalPosition).UseIdentityAlwaysColumn();
        builder.HasIndex(e => e.GlobalPosition).IsUnique();
        builder.Property(e => e.EventType).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Metadata).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
        builder.HasOne<StreamRow>()
            .WithMany()
            .HasForeignKey(e => e.StreamId);
        builder.Property(e => e.Xid)
            .HasColumnType("xid8")
            .HasDefaultValueSql("pg_current_xact_id()");
    }
}
