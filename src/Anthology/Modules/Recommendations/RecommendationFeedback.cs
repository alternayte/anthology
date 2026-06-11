using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Recommendations;

public sealed class RecommendationFeedback
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TitleId { get; set; }
    public FeedbackSignal Signal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RecommendationFeedbackConfiguration : IEntityTypeConfiguration<RecommendationFeedback>
{
    public void Configure(EntityTypeBuilder<RecommendationFeedback> builder)
    {
        builder.ToTable("feedback", "recommendations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Signal).HasConversion(new SnakeCaseEnumConverter<FeedbackSignal>());
        builder.HasIndex(e => new { e.UserId, e.TitleId, e.CreatedAt });
    }
}
