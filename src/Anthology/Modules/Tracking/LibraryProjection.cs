using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Tracking;

public sealed class LibraryItem
{
    public Guid UserId { get; set; }
    public Guid TitleId { get; set; }
    public MediaType MediaType { get; set; } = MediaType.Film;
    public string Title { get; set; } = default!;
    public TrackedStatus Status { get; set; }
    public int? Rating { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Private;
}

internal sealed class LibraryItemConfiguration : IEntityTypeConfiguration<LibraryItem>
{
    public void Configure(EntityTypeBuilder<LibraryItem> builder)
    {
        builder.ToTable("library_items", "tracking");
        builder.HasKey(e => new { e.UserId, e.TitleId });
        builder.HasIndex(e => new { e.UserId, e.AddedAt, e.TitleId }).IsDescending(false, true, false);
        builder.HasIndex(e => new { e.UserId, e.Rating, e.TitleId }).IsDescending(false, true, false);
        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Status).HasConversion(new SnakeCaseEnumConverter<TrackedStatus>());
        builder.Property(e => e.MediaType).HasConversion(new SnakeCaseEnumConverter<MediaType>());
        builder.Property(e => e.Visibility).HasConversion(new SnakeCaseEnumConverter<Visibility>());
    }
}

public sealed class LibraryProjection(TrackingDbContext db) : IProjection, IDbContextProjection, IRebuildableProjection
{
    public DbContext DbContext => db;
    public static string SchemaQualifiedTableName => "tracking.library_items";

    public async Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            if (envelope.UserId is null || envelope.ContextId is null) continue;

            switch (envelope.Event)
            {
                case ItemWanted w:
                    var mediaStr = SnakeCaseEnumHelper.ToSnakeCase(Enum.Parse<MediaType>(w.MediaType, true));
                    var statusStr = SnakeCaseEnumHelper.ToSnakeCase(TrackedStatus.WantToConsume);
                    var visibilityStr = SnakeCaseEnumHelper.ToSnakeCase(Visibility.Private);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO tracking.library_items (user_id, title_id, media_type, title, status, added_at, visibility)
                        VALUES ({envelope.UserId.Value}, {w.TitleId}, {mediaStr}, {w.TitleName}, {statusStr}, {w.At}, {visibilityStr})
                        ON CONFLICT (user_id, title_id) DO NOTHING
                        """, ct);
                    break;

                case ItemStarted:
                    await Upsert(envelope, item => item.Status = TrackedStatus.InProgress, ct);
                    break;

                case ItemFinished f:
                    await Upsert(envelope, item =>
                    {
                        item.Status = TrackedStatus.Finished;
                        item.Rating = f.Rating?.Value;
                        item.FinishedAt = f.At;
                    }, ct);
                    break;

                case ItemAbandoned:
                    await Upsert(envelope, item => item.Status = TrackedStatus.Abandoned, ct);
                    break;

                case ItemRerated r:
                    await Upsert(envelope, item => item.Rating = r.Rating.Value, ct);
                    break;
            }
        }
    }

    private async Task Upsert(EventEnvelope envelope, Action<LibraryItem> update, CancellationToken ct)
    {
        var item = await db.LibraryItems.FindAsync([envelope.UserId!.Value, envelope.ContextId!.Value], ct);
        if (item is not null)
            update(item);
    }
}
