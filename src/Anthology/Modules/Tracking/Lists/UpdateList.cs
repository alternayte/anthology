using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using FluentValidation;

namespace Anthology.Modules.Tracking;

public static class UpdateList
{
    public sealed record Command(string? Name, string? Description, bool DescriptionProvided, ListVisibility? Visibility,
        Guid UserId, Guid ListId, DateTimeOffset At) : ICommand<Result<CuratedListDto>>, ICuratedListCommand
    {
        public Guid StreamId => ListId;
        public (Guid? UserId, Guid? ContextId) GetCorrelationHints() => (UserId, null);
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
            RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        }
    }

    public sealed class Handler(EventStore store, InlineProjector projector, OutboxWriter outboxWriter)
        : EventSourcedHandler<Command, CuratedListState, CuratedListDto>(
            store, projector, outboxWriter,
            CuratedList.Decide, CuratedList.Evolve,
            (streamId, cmd, state) => new CuratedListDto(streamId, state.Name, state.Description, state.Visibility, state.Items.Count));
}
