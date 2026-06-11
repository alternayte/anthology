namespace Anthology.Modules.Recommendations;

public static class FeedbackResolver
{
    /// <summary>
    /// Assumes all <paramref name="rows"/> belong to a single user — the caller owns the <c>WHERE user_id = …</c> filter.
    /// Mixing rows from multiple users would collapse their signals into one map and produce incorrect results.
    /// </summary>
    public static IReadOnlyDictionary<Guid, FeedbackSignal> Resolve(
        IEnumerable<(Guid TitleId, FeedbackSignal Signal, DateTimeOffset CreatedAt)> rows) =>
        rows
            .GroupBy(r => r.TitleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.CreatedAt).First().Signal);

    public static IReadOnlySet<Guid> Excluded(IReadOnlyDictionary<Guid, FeedbackSignal> resolved) =>
        resolved
            .Where(kv => kv.Value is FeedbackSignal.Hidden or FeedbackSignal.Seen)
            .Select(kv => kv.Key)
            .ToHashSet();

    public static IReadOnlySet<Guid> Promoted(IReadOnlyDictionary<Guid, FeedbackSignal> resolved) =>
        resolved
            .Where(kv => kv.Value is FeedbackSignal.MoreLikeThis)
            .Select(kv => kv.Key)
            .ToHashSet();
}
