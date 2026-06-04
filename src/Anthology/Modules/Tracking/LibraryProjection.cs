using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Tracking;

public sealed class LibraryItem
{
    public Guid UserId { get; set; }
    public Guid TitleId { get; set; }
    public string MediaType { get; set; } = "film";
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int? Rating { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Visibility { get; set; } = "private";
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
    }
}

public sealed class LibraryProjection(TrackingDbContext db) : IProjection, IDbContextProjection
{
    public DbContext DbContext => db;

    public async Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            if (envelope.UserId is null || envelope.TitleId is null) continue;

            switch (envelope.Event)
            {
                case ItemWanted w:
                    db.LibraryItems.Add(new LibraryItem
                    {
                        UserId = envelope.UserId.Value,
                        TitleId = w.TitleId,
                        MediaType = w.MediaType,
                        Title = w.TitleName,
                        Status = "want_to_consume",
                        AddedAt = w.At,
                    });
                    break;

                case ItemStarted:
                    await Upsert(envelope, item => item.Status = "in_progress", ct);
                    break;

                case ItemFinished f:
                    await Upsert(envelope, item =>
                    {
                        item.Status = "finished";
                        item.Rating = f.Rating?.Value;
                        item.FinishedAt = f.At;
                    }, ct);
                    break;

                case ItemAbandoned:
                    await Upsert(envelope, item => item.Status = "abandoned", ct);
                    break;

                case ItemRerated r:
                    await Upsert(envelope, item => item.Rating = r.Rating.Value, ct);
                    break;
            }
        }
    }

    private async Task Upsert(EventEnvelope envelope, Action<LibraryItem> update, CancellationToken ct)
    {
        var item = await db.LibraryItems.FindAsync([envelope.UserId!.Value, envelope.TitleId!.Value], ct);
        if (item is not null)
            update(item);
    }
}
