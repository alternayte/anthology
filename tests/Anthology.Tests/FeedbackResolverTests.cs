using Anthology.Modules.Recommendations;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public sealed class FeedbackResolverTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Latest_signal_per_title_wins()
    {
        var rows = new[]
        {
            (A, FeedbackSignal.Hidden, T0),
            (A, FeedbackSignal.Restored, T0.AddMinutes(1)),
            (B, FeedbackSignal.MoreLikeThis, T0),
        };

        var resolved = FeedbackResolver.Resolve(rows);

        resolved[A].Should().Be(FeedbackSignal.Restored);
        resolved[B].Should().Be(FeedbackSignal.MoreLikeThis);
    }

    [Fact]
    public void Excluded_set_contains_only_hidden_and_seen()
    {
        var rows = new[]
        {
            (A, FeedbackSignal.Hidden, T0),
            (B, FeedbackSignal.Seen, T0),
        };

        var resolved = FeedbackResolver.Resolve(rows);

        FeedbackResolver.Excluded(resolved).Should().BeEquivalentTo(new[] { A, B });
    }

    [Fact]
    public void Restored_removes_title_from_excluded_set()
    {
        var rows = new[]
        {
            (A, FeedbackSignal.Hidden, T0),
            (A, FeedbackSignal.Restored, T0.AddMinutes(1)),
        };

        var resolved = FeedbackResolver.Resolve(rows);

        FeedbackResolver.Excluded(resolved).Should().BeEmpty();
    }

    [Fact]
    public void Promoted_set_contains_only_more_like_this()
    {
        var rows = new[]
        {
            (A, FeedbackSignal.MoreLikeThis, T0),
            (B, FeedbackSignal.Hidden, T0),
        };

        var resolved = FeedbackResolver.Resolve(rows);

        FeedbackResolver.Promoted(resolved).Should().BeEquivalentTo(new[] { A });
    }
}
