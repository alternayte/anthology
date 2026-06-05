using Anthology.Kernel;

namespace Anthology.Modules.Tracking;

public enum TrackedStatus { None, WantToConsume, InProgress, Finished, Abandoned, Rerated }

public enum Visibility { Private, Public }

public readonly record struct Rating
{
    public int Value { get; init; }

    public Rating(int value)
    {
        if (value is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(value), "Rating must be between 1 and 10.");
        Value = value;
    }

    public static implicit operator int(Rating r) => r.Value;
    public static explicit operator Rating(int v) => new(v);
}

public sealed record ItemWanted(Guid TitleId, string TitleName, string MediaType, DateTimeOffset At) : IDomainEvent;
public sealed record ItemStarted(DateTimeOffset At) : IDomainEvent;
public sealed record ItemFinished(Rating? Rating, DateTimeOffset At) : IDomainEvent;
public sealed record ItemAbandoned(DateTimeOffset At) : IDomainEvent;
public sealed record ItemRerated(Rating Rating, DateTimeOffset At) : IDomainEvent;

public sealed record TrackedItemState(TrackedStatus Status, Rating? Rating, Guid TitleId, int Version)
{
    public static readonly TrackedItemState Initial = new(TrackedStatus.None, null, Guid.Empty, 0);
}

public interface ITrackingCommand : IEventSourcedCommand;

public static class TrackedItem
{
    public static Result<IReadOnlyList<IDomainEvent>> Decide(TrackedItemState state, ITrackingCommand command) =>
        command switch
        {
            WantItem.Command c => HandleWant(state, c),
            StartItem.Command c => HandleStart(state, c),
            FinishItem.Command c => HandleFinish(state, c),
            AbandonItem.Command c => HandleAbandon(state, c),
            RerateItem.Command c => HandleRerate(state, c),
            _ => Error.Unprocessable("tracking.unknown_command", "Unrecognised command.")
        };

    public static TrackedItemState Evolve(TrackedItemState state, IDomainEvent e) => e switch
    {
        ItemWanted w => state with { Status = TrackedStatus.WantToConsume, TitleId = w.TitleId, Version = state.Version + 1 },
        ItemStarted => state with { Status = TrackedStatus.InProgress, Version = state.Version + 1 },
        ItemFinished f => state with { Status = TrackedStatus.Finished, Rating = f.Rating, Version = state.Version + 1 },
        ItemAbandoned => state with { Status = TrackedStatus.Abandoned, Version = state.Version + 1 },
        ItemRerated r => state with { Rating = r.Rating, Version = state.Version + 1 },
        _ => state
    };

    private static Result<IReadOnlyList<IDomainEvent>> HandleWant(TrackedItemState state, WantItem.Command c) =>
        state.Status is not TrackedStatus.None
            ? Error.Conflict("tracking.already_tracked", "Item is already being tracked.")
            : Ok(new ItemWanted(c.TitleId, c.TitleName, c.MediaType, c.At));

    private static Result<IReadOnlyList<IDomainEvent>> HandleStart(TrackedItemState state, StartItem.Command c) =>
        state.Status switch
        {
            TrackedStatus.None => Error.Conflict("tracking.not_tracked", "Add the item first."),
            TrackedStatus.InProgress => Error.Conflict("tracking.already_in_progress", "Already in progress."),
            TrackedStatus.Finished => Error.Conflict("tracking.already_finished", "Already finished."),
            _ => Ok(new ItemStarted(c.At))
        };

    private static Result<IReadOnlyList<IDomainEvent>> HandleFinish(TrackedItemState state, FinishItem.Command c) =>
        state.Status switch
        {
            TrackedStatus.None => Error.Conflict("tracking.not_tracked", "Add the item first."),
            TrackedStatus.Finished => Error.Conflict("tracking.already_finished", "Already finished."),
            _ => Ok(new ItemFinished(c.Rating.HasValue ? new Rating(c.Rating.Value) : null, c.At))
        };

    private static Result<IReadOnlyList<IDomainEvent>> HandleAbandon(TrackedItemState state, AbandonItem.Command c) =>
        state.Status switch
        {
            TrackedStatus.None => Error.Conflict("tracking.not_tracked", "Add the item first."),
            TrackedStatus.Abandoned => Error.Conflict("tracking.already_abandoned", "Already abandoned."),
            TrackedStatus.Finished => Error.Conflict("tracking.already_finished", "Cannot abandon a finished item."),
            _ => Ok(new ItemAbandoned(c.At))
        };

    private static Result<IReadOnlyList<IDomainEvent>> HandleRerate(TrackedItemState state, RerateItem.Command c) =>
        state.Status is not TrackedStatus.Finished
            ? Error.Conflict("tracking.not_finished", "Can only re-rate a finished item.")
            : Ok(new ItemRerated(new Rating(c.Rating), c.At));

    private static Result<IReadOnlyList<IDomainEvent>> Ok(IDomainEvent e) =>
        Result<IReadOnlyList<IDomainEvent>>.FromValue(new List<IDomainEvent> { e });
}

public sealed record TrackedItemDto(Guid StreamId, Guid TitleId, TrackedStatus Status, Rating? Rating);
