using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
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

public sealed class DiaryProjection(TrackingDbContext db) : IProjection, IDbContextProjection
{
    public DbContext DbContext => db;

    public async Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            if (envelope.UserId is null || envelope.ContextId is null) continue;

            var entry = envelope.Event switch
            {
                ItemWanted w => new DiaryEntry
                {
                    UserId = envelope.UserId.Value,
                    TitleId = w.TitleId,
                    Status = TrackedStatus.WantToConsume,
                    OccurredAt = w.At,
                },
                ItemStarted s => new DiaryEntry
                {
                    UserId = envelope.UserId.Value,
                    TitleId = envelope.ContextId.Value,
                    Status = TrackedStatus.InProgress,
                    OccurredAt = s.At,
                },
                ItemFinished f => new DiaryEntry
                {
                    UserId = envelope.UserId.Value,
                    TitleId = envelope.ContextId.Value,
                    Status = TrackedStatus.Finished,
                    Rating = f.Rating?.Value,
                    OccurredAt = f.At,
                },
                ItemAbandoned a => new DiaryEntry
                {
                    UserId = envelope.UserId.Value,
                    TitleId = envelope.ContextId.Value,
                    Status = TrackedStatus.Abandoned,
                    OccurredAt = a.At,
                },
                ItemRerated r => new DiaryEntry
                {
                    UserId = envelope.UserId.Value,
                    TitleId = envelope.ContextId.Value,
                    Status = TrackedStatus.Rerated,
                    Rating = r.Rating.Value,
                    OccurredAt = r.At,
                },
                _ => null
            };

            if (entry is not null)
                db.DiaryEntries.Add(entry);
        }
    }
}
