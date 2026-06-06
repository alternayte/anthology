using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;

namespace Anthology.Modules.Tracking;

public static class ReorderItem
{
    public sealed record Command(Guid TitleId, Guid? AfterTitleId, Guid UserId, Guid ListId, DateTimeOffset At)
        : ICommand<Result<CuratedListDto>>, ICuratedListCommand
    {
        public Guid StreamId => ListId;
        public (Guid? UserId, Guid? ContextId) GetCorrelationHints() => (UserId, null);
    }

    public sealed class Handler(EventStore store, InlineProjector projector, OutboxWriter outboxWriter)
        : EventSourcedHandler<Command, CuratedListState, CuratedListDto>(
            store, projector, outboxWriter,
            CuratedList.Decide, CuratedList.Evolve,
            (streamId, cmd, state) => new CuratedListDto(streamId, state.Name, state.Description, state.Visibility, state.Items.Count));
}
