using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
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

public sealed class ListProjection(TrackingDbContext db, CatalogDbContext catalogDb)
    : IProjection, IDbContextProjection, IRebuildableProjection
{
    public static string SchemaQualifiedTableName => "tracking.lists";
    public DbContext DbContext => db;

    public async Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            if (envelope.UserId is null) continue;

            switch (envelope.Event)
            {
                case ListCreated c:
                {
                    var visibilityStr = c.Visibility.ToSnakeCase();
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO tracking.lists (list_id, user_id, name, description, visibility, item_count, created_at, is_deleted)
                        VALUES ({envelope.StreamId}, {c.UserId}, {c.Name}, {c.Description}, {visibilityStr}, {0}, {c.CreatedAt}, {false})
                        ON CONFLICT (list_id) DO NOTHING
                        """, ct);
                    break;
                }

                case ListRenamed r:
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET name = {r.Name} WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ListDescriptionChanged d:
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET description = {d.Description} WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ListVisibilityChanged v:
                {
                    var visibilityStr = v.Visibility.ToSnakeCase();
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET visibility = {visibilityStr} WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ListDeleted:
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET is_deleted = true WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ItemAddedToList a:
                {
                    var title = await catalogDb.Titles.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.TitleId == a.TitleId, ct);

                    var titleName = title?.Name ?? "Unknown";
                    var mediaType = title?.MediaType.ToSnakeCase() ?? "film";
                    var posterPath = title?.PosterPath;

                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO tracking.list_items (list_id, title_id, position, title, media_type, poster_path, added_at)
                        VALUES ({envelope.StreamId}, {a.TitleId}, {a.Position}, {titleName}, {mediaType}, {posterPath}, {a.AddedAt})
                        ON CONFLICT (list_id, title_id) DO NOTHING
                        """, ct);

                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET item_count = (
                            SELECT COUNT(*) FROM tracking.list_items WHERE list_id = {envelope.StreamId}
                        ) WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ItemRemovedFromList r:
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        DELETE FROM tracking.list_items WHERE list_id = {envelope.StreamId} AND title_id = {r.TitleId}
                        """, ct);

                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.lists SET item_count = (
                            SELECT COUNT(*) FROM tracking.list_items WHERE list_id = {envelope.StreamId}
                        ) WHERE list_id = {envelope.StreamId}
                        """, ct);
                    break;
                }

                case ListItemReordered o:
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE tracking.list_items SET position = {o.NewPosition}
                        WHERE list_id = {envelope.StreamId} AND title_id = {o.TitleId}
                        """, ct);
                    break;
                }
            }
        }
    }
}
