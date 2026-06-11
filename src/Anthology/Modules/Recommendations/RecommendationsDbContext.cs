using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Recommendations;

public sealed class RecommendationsDbContext(DbContextOptions<RecommendationsDbContext> options) : DbContext(options)
{
    public DbSet<RecommendationFeedback> Feedback => Set<RecommendationFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("recommendations");
        modelBuilder.ApplyConfiguration(new RecommendationFeedbackConfiguration());
    }
}
