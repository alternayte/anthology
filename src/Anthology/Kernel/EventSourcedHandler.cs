using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;

namespace Anthology.Kernel;

public class EventSourcedHandler<TCommand, TState, TResponse>(
    EventStore.EventStore store,
    InlineProjector projector,
    OutboxWriter outboxWriter,
    Func<TState, TCommand, Result<IReadOnlyList<IDomainEvent>>> decide,
    Func<TState, IDomainEvent, TState> evolve,
    Func<Guid, TCommand, TState, TResponse> mapResponse)
    : ICommandHandler<TCommand, Result<TResponse>>
    where TCommand : IEventSourcedCommand
    where TState : IAggregateState<TState>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct)
    {
        var streamId = command.StreamId;
        var (loaded, version) = await store.LoadAsync<TState>(streamId, ct);
        var state = loaded ?? TState.Initial;

        var result = decide(state, command);
        if (result.IsError) return Result<TResponse>.FromError(result.Error);

        var newState = result.Value.Aggregate(state, evolve);
        var (hintUserId, hintContextId) = command.GetCorrelationHints();
        var meta = new EventMetadata(Guid.NewGuid(), null, command.UserId, command.At);
        var envelopes = await store.AppendAsync(
            streamId, TState.StreamType, version, result.Value, newState, meta, ct,
            hintUserId, hintContextId);

        projector.Stage(envelopes);
        outboxWriter.Stage(envelopes);

        return mapResponse(streamId, command, newState);
    }
}
