using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;

namespace Anthology.Modules.Tracking;

public static class StartItem
{
    public sealed record Command(DateTimeOffset At, Guid UserId = default, Guid TitleId = default)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand
    {
        public Guid StreamId => Kernel.StreamId.For(UserId, TitleId);
        public (Guid? UserId, Guid? ContextId) GetCorrelationHints() => (UserId, TitleId);
    }

    public sealed class Handler(EventStore store, InlineProjector projector, OutboxWriter outboxWriter)
        : EventSourcedHandler<Command, TrackedItemState, TrackedItemDto>(
            store, projector, outboxWriter,
            TrackedItem.Decide, TrackedItem.Evolve,
            "tracked_item", TrackedItemState.Initial,
            (streamId, cmd, state) => new TrackedItemDto(streamId, cmd.TitleId, state.Status, state.Rating));
}
