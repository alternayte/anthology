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

public sealed class LibraryProjection(TrackingDbContext db) : IProjection, IDbContextProjection
{
    public DbContext DbContext => db;

    public async Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            if (envelope.UserId is null || envelope.ContextId is null) continue;

            switch (envelope.Event)
            {
                case ItemWanted w:
                    db.LibraryItems.Add(new LibraryItem
                    {
                        UserId = envelope.UserId.Value,
                        TitleId = w.TitleId,
                        MediaType = Enum.Parse<MediaType>(w.MediaType, true),
                        Title = w.TitleName,
                        Status = TrackedStatus.WantToConsume,
                        AddedAt = w.At,
                    });
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
