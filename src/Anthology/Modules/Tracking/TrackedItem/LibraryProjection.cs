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
    public int? PartsCompleted { get; set; }
    public int? PartsTotal { get; set; }
    public string? PosterPath { get; set; }
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

public sealed class LibraryProjection(TrackingDbContext db, CatalogDbContext catalogDb)
    : IProjection, IDbContextProjection, IRebuildableProjection
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
                    var mediaType = Enum.Parse<MediaType>(w.MediaType, true);
                    var mediaStr = mediaType.ToSnakeCase();
                    var statusStr = TrackedStatus.WantToConsume.ToSnakeCase();
                    var visibilityStr = Visibility.Private.ToSnakeCase();
                    var posterPath = await catalogDb.Titles.AsNoTracking()
                        .Where(t => t.TitleId == w.TitleId)
                        .Select(t => t.PosterPath)
                        .FirstOrDefaultAsync(ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO tracking.library_items (user_id, title_id, media_type, title, status, added_at, visibility, poster_path)
                        VALUES ({envelope.UserId.Value}, {w.TitleId}, {mediaStr}, {w.TitleName}, {statusStr}, {w.At}, {visibilityStr}, {posterPath})
                        ON CONFLICT (user_id, title_id) DO NOTHING
                        """, ct);

                    if (mediaType == MediaType.Episode)
                        await UpsertShowSummaryAsync(envelope.UserId.Value, w.TitleId, TrackedStatus.WantToConsume, ct);
                    break;

                case ItemStarted:
                    await Upsert(envelope, item => item.Status = TrackedStatus.InProgress, ct);

                    if (await IsEpisodeAsync(envelope.ContextId.Value, ct))
                    {
                        // Flush the episode status change before counting finished episodes
                        await db.SaveChangesAsync(ct);
                        await UpsertShowSummaryAsync(envelope.UserId.Value, envelope.ContextId.Value, TrackedStatus.InProgress, ct);
                    }
                    break;

                case ItemFinished f:
                    await Upsert(envelope, item =>
                    {
                        item.Status = TrackedStatus.Finished;
                        item.Rating = f.Rating?.Value;
                        item.FinishedAt = f.At;
                    }, ct);

                    if (await IsEpisodeAsync(envelope.ContextId.Value, ct))
                    {
                        // Flush the episode status change before counting finished episodes
                        await db.SaveChangesAsync(ct);
                        await UpsertShowSummaryAsync(envelope.UserId.Value, envelope.ContextId.Value, TrackedStatus.Finished, ct);
                    }
                    break;

                case ItemAbandoned:
                    await Upsert(envelope, item => item.Status = TrackedStatus.Abandoned, ct);
                    break;

                case ItemRated r:
                    await Upsert(envelope, item => item.Rating = r.Rating.Value, ct);
                    break;
            }
        }
    }

    private async Task<bool> IsEpisodeAsync(Guid titleId, CancellationToken ct)
    {
        var title = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.TitleId == titleId)
            .Select(t => new { t.MediaType })
            .FirstOrDefaultAsync(ct);
        return title?.MediaType == MediaType.Episode;
    }

    private async Task UpsertShowSummaryAsync(
        Guid userId, Guid episodeTitleId, TrackedStatus episodeEventStatus, CancellationToken ct)
    {
        // Resolve show: episode → season → show
        var episode = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.TitleId == episodeTitleId && t.MediaType == MediaType.Episode)
            .Select(t => new { t.ParentTitleId })
            .FirstOrDefaultAsync(ct);
        if (episode?.ParentTitleId is null) return;

        var season = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.TitleId == episode.ParentTitleId && t.MediaType == MediaType.Season)
            .Select(t => new { t.TitleId, t.ParentTitleId, t.Name })
            .FirstOrDefaultAsync(ct);
        if (season?.ParentTitleId is null) return;

        var show = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.TitleId == season.ParentTitleId && t.MediaType == MediaType.TvShow)
            .Select(t => new { t.TitleId, t.Name, t.PosterPath })
            .FirstOrDefaultAsync(ct);
        if (show is null) return;

        // Count total episodes and finished episodes for this show in the user's library
        var seasonIds = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.ParentTitleId == show.TitleId && t.MediaType == MediaType.Season)
            .Select(t => t.TitleId)
            .ToListAsync(ct);

        var episodeIds = await catalogDb.Titles.AsNoTracking()
            .Where(t => seasonIds.Contains(t.ParentTitleId!.Value) && t.MediaType == MediaType.Episode)
            .Select(t => t.TitleId)
            .ToListAsync(ct);

        var partsTotal = episodeIds.Count;

        var partsCompleted = await db.LibraryItems.AsNoTracking()
            .CountAsync(li =>
                li.UserId == userId &&
                episodeIds.Contains(li.TitleId) &&
                li.Status == TrackedStatus.Finished, ct);

        // Determine show status
        TrackedStatus showStatus;
        var existing = await db.LibraryItems.AsNoTracking()
            .Where(li => li.UserId == userId && li.TitleId == show.TitleId)
            .Select(li => new { li.Status })
            .FirstOrDefaultAsync(ct);

        if (partsTotal > 0 && partsCompleted == partsTotal)
            showStatus = TrackedStatus.Finished;
        else if (episodeEventStatus is TrackedStatus.InProgress or TrackedStatus.Finished)
            showStatus = TrackedStatus.InProgress;
        else if (existing is not null)
            showStatus = existing.Status;
        else
            showStatus = TrackedStatus.WantToConsume;

        var showStatusStr = showStatus.ToSnakeCase();
        var showMediaStr = MediaType.TvShow.ToSnakeCase();
        var visibilityStr = Visibility.Private.ToSnakeCase();
        var now = DateTimeOffset.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tracking.library_items
                (user_id, title_id, media_type, title, status, added_at, visibility, parts_completed, parts_total, poster_path)
            VALUES
                ({userId}, {show.TitleId}, {showMediaStr}, {show.Name}, {showStatusStr}, {now}, {visibilityStr}, {partsCompleted}, {partsTotal}, {show.PosterPath})
            ON CONFLICT (user_id, title_id) DO UPDATE
                SET status = EXCLUDED.status,
                    parts_completed = EXCLUDED.parts_completed,
                    parts_total = EXCLUDED.parts_total,
                    poster_path = EXCLUDED.poster_path
            """, ct);
    }

    private async Task Upsert(EventEnvelope envelope, Action<LibraryItem> update, CancellationToken ct)
    {
        var item = await db.LibraryItems.FindAsync([envelope.UserId!.Value, envelope.ContextId!.Value], ct);
        if (item is not null)
            update(item);
    }
}
