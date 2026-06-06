using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<ListRow> Lists => Set<ListRow>();
    public DbSet<ListItemRow> ListItems => Set<ListItemRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tracking");
        modelBuilder.ApplyConfiguration(new DiaryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new LibraryItemConfiguration());
        modelBuilder.ApplyConfiguration(new ListRowConfiguration());
        modelBuilder.ApplyConfiguration(new ListItemRowConfiguration());
    }
}
