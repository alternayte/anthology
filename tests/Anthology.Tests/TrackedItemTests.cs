using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Modules.Tracking;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public class TrackedItemTests
{
    private static readonly Guid TitleId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static TrackedItemState Given(params IDomainEvent[] events) =>
        events.Aggregate(TrackedItemState.Initial, TrackedItem.Evolve);

    private static Result<IReadOnlyList<IDomainEvent>> When(TrackedItemState state, ITrackingCommand command) =>
        TrackedItem.Decide(state, command);

    [Fact]
    public void Want_on_new_stream_emits_ItemWanted()
    {
        var state = Given();
        var result = When(state, new WantItem.Command(TitleId, "The Matrix", "film", Guid.NewGuid(), Now));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemWanted>()
            .Which.TitleId.Should().Be(TitleId);
    }

    [Fact]
    public void Want_on_already_tracked_returns_conflict()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now));
        var result = When(state, new WantItem.Command(TitleId, "The Matrix", "film", Guid.NewGuid(), Now));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Start_on_wanted_emits_ItemStarted()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now));
        var result = When(state, new StartItem.Command(Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemStarted>();
    }

    [Fact]
    public void Start_on_new_stream_returns_conflict()
    {
        var state = Given();
        var result = When(state, new StartItem.Command(Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Finish_on_in_progress_emits_ItemFinished_with_rating()
    {
        var rating = new Rating(8);
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemStarted(Now));
        var result = When(state, new FinishItem.Command(rating, Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeFalse();
        var finished = result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemFinished>().Subject;
        finished.Rating.Should().Be(rating);
    }

    [Fact]
    public void Finish_on_wanted_skipping_start_is_allowed()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now));
        var result = When(state, new FinishItem.Command(null, Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Finish_on_new_stream_returns_conflict()
    {
        var state = Given();
        var result = When(state, new FinishItem.Command(null, Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Finish_on_already_finished_returns_conflict()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemFinished(null, Now));
        var result = When(state, new FinishItem.Command(null, Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Abandon_on_in_progress_emits_ItemAbandoned()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemStarted(Now));
        var result = When(state, new AbandonItem.Command(Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemAbandoned>();
    }

    [Fact]
    public void Abandon_on_finished_returns_conflict()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemFinished(null, Now));
        var result = When(state, new AbandonItem.Command(Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Rerate_on_finished_emits_ItemRerated()
    {
        var newRating = new Rating(9);
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemFinished(new Rating(7), Now));
        var result = When(state, new RerateItem.Command(newRating, Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<ItemRerated>()
            .Which.Rating.Should().Be(newRating);
    }

    [Fact]
    public void Rerate_on_non_finished_returns_conflict()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemStarted(Now));
        var result = When(state, new RerateItem.Command(new Rating(5), Now, Guid.NewGuid(), TitleId));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Evolve_tracks_version()
    {
        var state = Given(new ItemWanted(TitleId, "The Matrix", "film", Now), new ItemStarted(Now));
        state.Version.Should().Be(2);
        state.Status.Should().Be(TrackedStatus.InProgress);
    }

    [Fact]
    public void Rating_rejects_out_of_range()
    {
        var act = () => new Rating(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
        var act2 = () => new Rating(11);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rating_accepts_valid_values()
    {
        var r = new Rating(5);
        ((int)r).Should().Be(5);
    }

    [Fact]
    public void Tracking_commands_provide_stream_id_and_correlation_hints()
    {
        var userId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var command = new WantItem.Command(titleId, "Test", "film", userId, DateTimeOffset.UtcNow);

        var esCommand = (IEventSourcedCommand)command;
        esCommand.StreamId.Should().Be(StreamId.For(userId, titleId));

        var (hintUserId, hintContextId) = esCommand.GetCorrelationHints();
        hintUserId.Should().Be(userId);
        hintContextId.Should().Be(titleId);
    }
}
