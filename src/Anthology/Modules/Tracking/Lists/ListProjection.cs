using Anthology.Kernel;
using Anthology.Modules.Catalog;
using Deedbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Tracking;

public sealed class ListRow
{
    public Guid ListId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public ListVisibility Visibility { get; set; } = ListVisibility.Private;
    public int ItemCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class ListRowConfiguration : IEntityTypeConfiguration<ListRow>
{
    public void Configure(EntityTypeBuilder<ListRow> builder)
    {
        builder.ToTable("lists", "tracking");
        builder.HasKey(e => e.ListId);
        builder.HasIndex(e => new { e.UserId, e.CreatedAt }).IsDescending(false, true);
        builder.Property(e => e.Visibility).HasConversion(new SnakeCaseEnumConverter<ListVisibility>());
    }
}

public sealed class ListItemRow
{
    public Guid ListId { get; set; }
    public Guid TitleId { get; set; }
    public double Position { get; set; }
    public string Title { get; set; } = default!;
    public string MediaType { get; set; } = default!;
    public string? PosterPath { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

internal sealed class ListItemRowConfiguration : IEntityTypeConfiguration<ListItemRow>
{
    public void Configure(EntityTypeBuilder<ListItemRow> builder)
    {
        builder.ToTable("list_items", "tracking");
        builder.HasKey(e => new { e.ListId, e.TitleId });
        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.MediaType).IsRequired();
    }
}

public sealed class ListProjection : Projection<TrackingDbContext>
{
    public ListProjection()
    {
        On<ListCreated>((c, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            var visibilityStr = c.Visibility.ToSnakeCase();
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking.lists (list_id, user_id, name, description, visibility, item_count, created_at, is_deleted)
                VALUES ({listId}, {c.UserId}, {c.Name}, {c.Description}, {visibilityStr}, {0}, {c.CreatedAt}, {false})
                ON CONFLICT (list_id) DO NOTHING
                """, ctx.CancellationToken);
        });

        On<ListRenamed>((r, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.lists SET name = {r.Name} WHERE list_id = {listId}
                """, ctx.CancellationToken);
        });

        On<ListDescriptionChanged>((d, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.lists SET description = {d.Description} WHERE list_id = {listId}
                """, ctx.CancellationToken);
        });

        On<ListVisibilityChanged>((v, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            var visibilityStr = v.Visibility.ToSnakeCase();
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.lists SET visibility = {visibilityStr} WHERE list_id = {listId}
                """, ctx.CancellationToken);
        });

        On<ListDeleted>((_, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.lists SET is_deleted = true WHERE list_id = {listId}
                """, ctx.CancellationToken);
        });

        On<ItemAddedToList>(async (a, ctx) =>
        {
            var (db, ct) = (ctx.Db, ctx.CancellationToken);
            var listId = Guid.Parse(ctx.StreamId);
            var title = await ctx.Services.GetRequiredService<CatalogDbContext>().Titles.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TitleId == a.TitleId, ct);

            var titleName = title?.Name ?? "Unknown";
            var mediaType = title?.MediaType.ToSnakeCase() ?? "film";
            var posterPath = title?.PosterPath;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking.list_items (list_id, title_id, position, title, media_type, poster_path, added_at)
                VALUES ({listId}, {a.TitleId}, {a.Position}, {titleName}, {mediaType}, {posterPath}, {a.AddedAt})
                ON CONFLICT (list_id, title_id) DO NOTHING
                """, ct);

            await CountItemsAsync(db, listId, ct);
        });

        On<ItemRemovedFromList>(async (r, ctx) =>
        {
            var (db, ct) = (ctx.Db, ctx.CancellationToken);
            var listId = Guid.Parse(ctx.StreamId);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM tracking.list_items WHERE list_id = {listId} AND title_id = {r.TitleId}
                """, ct);

            await CountItemsAsync(db, listId, ct);
        });

        On<ListItemReordered>((o, ctx) =>
        {
            var listId = Guid.Parse(ctx.StreamId);
            return ctx.Db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking.list_items SET position = {o.NewPosition}
                WHERE list_id = {listId} AND title_id = {o.TitleId}
                """, ctx.CancellationToken);
        });
    }

    protected override async Task ResetAsync(WriteContext<TrackingDbContext> context)
    {
        await context.Db.Database.ExecuteSqlRawAsync("DELETE FROM tracking.list_items", context.CancellationToken);
        await context.Db.Database.ExecuteSqlRawAsync("DELETE FROM tracking.lists", context.CancellationToken);
    }

    private static Task CountItemsAsync(TrackingDbContext db, Guid listId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE tracking.lists SET item_count = (
                SELECT COUNT(*) FROM tracking.list_items WHERE list_id = {listId}
            ) WHERE list_id = {listId}
            """, ct);
}
