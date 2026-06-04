using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Title> Titles => Set<Title>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfiguration(new TitleConfiguration());
    }
}
