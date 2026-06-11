namespace Anthology.Modules.Recommendations;

public static class FeedbackResolver
{
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
