using Anthology.Kernel;
using Anthology.Modules.Catalog;
using Deedbox;
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

public sealed class LibraryProjection : Projection<TrackingDbContext>
{
    public LibraryProjection()
    {
        On<ItemWanted>(async (w, ctx) =>
        {
            var (db, catalogDb, ct) = (ctx.Db, Catalog(ctx), ctx.CancellationToken);
            var userId = TrackingMetadata.UserId(ctx.Metadata);
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
                VALUES ({userId}, {w.TitleId}, {mediaStr}, {w.TitleName}, {statusStr}, {w.At}, {visibilityStr}, {posterPath})
                ON CONFLICT (user_id, title_id) DO NOTHING
                """, ct);

            if (mediaType == MediaType.Episode)
                await UpsertShowSummaryAsync(db, catalogDb, userId, w.TitleId, TrackedStatus.WantToConsume, ct);
        });

        On<ItemStarted>(async (_, ctx) =>
        {
            var (userId, titleId, ct) = Item(ctx);
            var statusStr = TrackedStatus.InProgress.ToSnakeCase();
            await ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.library_items SET status = {statusStr}
                WHERE user_id = {userId} AND title_id = {titleId}
                """, ct);
            await SummariseShowAsync(ctx, TrackedStatus.InProgress);
        });

        On<ItemFinished>(async (f, ctx) =>
        {
            var (userId, titleId, ct) = Item(ctx);
            var statusStr = TrackedStatus.Finished.ToSnakeCase();
            await ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.library_items SET status = {statusStr}, rating = {f.Rating?.Value}, finished_at = {f.At}
                WHERE user_id = {userId} AND title_id = {titleId}
                """, ct);
            await SummariseShowAsync(ctx, TrackedStatus.Finished);
        });

        On<ItemAbandoned>((_, ctx) =>
        {
            var (userId, titleId, ct) = Item(ctx);
            var statusStr = TrackedStatus.Abandoned.ToSnakeCase();
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.library_items SET status = {statusStr}
                WHERE user_id = {userId} AND title_id = {titleId}
                """, ct);
        });

        On<ItemRated>((r, ctx) =>
        {
            var (userId, titleId, ct) = Item(ctx);
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.library_items SET rating = {r.Rating.Value}
                WHERE user_id = {userId} AND title_id = {titleId}
                """, ct);
        });
    }

    protected override Task ResetAsync(WriteContext<TrackingDbContext> context) =>
        context.Db.Database.ExecuteSqlRawAsync("DELETE FROM tracking.library_items", context.CancellationToken);

    private static CatalogDbContext Catalog(ProjectionContext<TrackingDbContext> ctx) =>
        ctx.Services.GetRequiredService<CatalogDbContext>();

    private static (Guid UserId, Guid TitleId, CancellationToken Ct) Item(ProjectionContext<TrackingDbContext> ctx) =>
        (TrackingMetadata.UserId(ctx.Metadata), TrackingMetadata.TitleId(ctx.Metadata), ctx.CancellationToken);

    private static async Task SummariseShowAsync(ProjectionContext<TrackingDbContext> ctx, TrackedStatus episodeStatus)
    {
        var (userId, titleId, ct) = Item(ctx);
        var catalogDb = Catalog(ctx);
        if (await IsEpisodeAsync(catalogDb, titleId, ct))
            await UpsertShowSummaryAsync(ctx.Db, catalogDb, userId, titleId, episodeStatus, ct);
    }

    private static async Task<bool> IsEpisodeAsync(CatalogDbContext catalogDb, Guid titleId, CancellationToken ct)
    {
        var title = await catalogDb.Titles.AsNoTracking()
            .Where(t => t.TitleId == titleId)
            .Select(t => new { t.MediaType })
            .FirstOrDefaultAsync(ct);
        return title?.MediaType == MediaType.Episode;
    }

    private static async Task UpsertShowSummaryAsync(
        TrackingDbContext db, CatalogDbContext catalogDb, Guid userId, Guid episodeTitleId, TrackedStatus episodeEventStatus, CancellationToken ct)
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
}
