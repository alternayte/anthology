using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using FluentValidation;

namespace Anthology.Modules.Tracking;

public static class RerateItem
{
    public sealed record Command(int Rating, DateTimeOffset At, Guid UserId = default, Guid TitleId = default)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand
    {
        public Guid StreamId => Kernel.StreamId.For(UserId, TitleId);
        public (Guid? UserId, Guid? ContextId) GetCorrelationHints() => (UserId, TitleId);
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Rating).InclusiveBetween(1, 10);
        }
    }

    public sealed class Handler(EventStore store, InlineProjector projector, OutboxWriter outboxWriter)
        : EventSourcedHandler<Command, TrackedItemState, TrackedItemDto>(
            store, projector, outboxWriter,
            TrackedItem.Decide, TrackedItem.Evolve,
            "tracked_item", TrackedItemState.Initial,
            (streamId, cmd, state) => new TrackedItemDto(streamId, cmd.TitleId, state.Status, state.Rating));
}
