using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Anthology.Modules.Catalog;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<TitleCredit> TitleCredits => Set<TitleCredit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfiguration(new TitleConfiguration());
        modelBuilder.ApplyConfiguration(new TitleCreditConfiguration());
    }
}
