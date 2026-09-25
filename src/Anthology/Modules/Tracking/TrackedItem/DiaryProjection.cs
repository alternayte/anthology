using Anthology.Kernel;
using Deedbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Tracking;

public sealed class DiaryEntry
{
    public Guid UserId { get; set; }
    public Guid TitleId { get; set; }
    public TrackedStatus Status { get; set; }
    public int? Rating { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Private;
}

internal sealed class DiaryEntryConfiguration : IEntityTypeConfiguration<DiaryEntry>
{
    public void Configure(EntityTypeBuilder<DiaryEntry> builder)
    {
        builder.ToTable("diary_entries", "tracking");
        builder.HasKey(e => new { e.UserId, e.TitleId, e.OccurredAt });
        builder.HasIndex(e => new { e.UserId, e.OccurredAt }).IsDescending(false, true);
        builder.Property(e => e.Status).HasConversion(new SnakeCaseEnumConverter<TrackedStatus>());
        builder.Property(e => e.Visibility).HasConversion(new SnakeCaseEnumConverter<Visibility>());
    }
}

public sealed class DiaryProjection : Projection<TrackingDbContext>
{
    public DiaryProjection()
    {
        On<ItemWanted>((e, ctx) => Insert(ctx, e.TitleId, TrackedStatus.WantToConsume, null, e.At));
        On<ItemStarted>((e, ctx) => Insert(ctx, TrackingMetadata.TitleId(ctx.Metadata), TrackedStatus.InProgress, null, e.At));
        On<ItemFinished>((e, ctx) => Insert(ctx, TrackingMetadata.TitleId(ctx.Metadata), TrackedStatus.Finished, e.Rating?.Value, e.At));
        On<ItemAbandoned>((e, ctx) => Insert(ctx, TrackingMetadata.TitleId(ctx.Metadata), TrackedStatus.Abandoned, null, e.At));
        On<ItemRated>((e, ctx) => Insert(ctx, TrackingMetadata.TitleId(ctx.Metadata), TrackedStatus.Rerated, e.Rating.Value, e.At));
    }

    protected override Task ResetAsync(WriteContext<TrackingDbContext> context) =>
        context.Db.Database.ExecuteSqlRawAsync("DELETE FROM tracking.diary_entries", context.CancellationToken);

    private static Task Insert(ProjectionContext<TrackingDbContext> ctx, Guid titleId, TrackedStatus status, int? rating, DateTimeOffset at)
    {
        var userId = TrackingMetadata.UserId(ctx.Metadata);
        var statusStr = status.ToSnakeCase();
        var visibilityStr = Visibility.Private.ToSnakeCase();
        return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tracking.diary_entries (user_id, title_id, status, rating, occurred_at, visibility)
            VALUES ({userId}, {titleId}, {statusStr}, {rating}, {at}, {visibilityStr})
            ON CONFLICT (user_id, title_id, occurred_at) DO NOTHING
            """, ctx.CancellationToken);
    }
}

internal static class EnumExtensions
{
    public static string ToSnakeCase<T>(this T value) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
