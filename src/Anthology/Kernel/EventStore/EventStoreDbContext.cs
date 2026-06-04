using Microsoft.EntityFrameworkCore;

namespace Anthology.Kernel.EventStore;

public sealed class EventStoreDbContext(DbContextOptions<EventStoreDbContext> options) : DbContext(options)
{
    public DbSet<EventRow> Events => Set<EventRow>();
    public DbSet<StreamRow> Streams => Set<StreamRow>();
    public DbSet<CheckpointRow> Checkpoints => Set<CheckpointRow>();
    public DbSet<OutboxRow> Outbox => Set<OutboxRow>();
    public DbSet<InboxRow> Inbox => Set<InboxRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("es");
        modelBuilder.ApplyConfiguration(new EventRowConfiguration());
        modelBuilder.ApplyConfiguration(new StreamRowConfiguration());
        modelBuilder.ApplyConfiguration(new CheckpointRowConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxRowConfiguration());
        modelBuilder.ApplyConfiguration(new InboxRowConfiguration());
    }
}
