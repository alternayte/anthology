namespace Anthology.Kernel.EventStore;

public sealed class StreamEvolverRegistry
{
    private readonly Dictionary<string, Func<IReadOnlyList<EventRow>, string>> _rebuilders = new();

    public void Register<TState>(
        EventSerializer serializer,
        Func<TState, IDomainEvent, TState> evolve)
        where TState : IAggregateState<TState>
    {
        _rebuilders[TState.StreamType] = eventRows =>
        {
            var state = TState.Initial;
            foreach (var row in eventRows)
            {
                var e = serializer.Deserialize(row.EventType, row.Payload);
                state = evolve(state, e);
            }
            return serializer.SerializeState(state);
        };
    }

    public Func<IReadOnlyList<EventRow>, string> GetRebuilder(string streamType) =>
        _rebuilders.TryGetValue(streamType, out var rebuilder)
            ? rebuilder
            : throw new InvalidOperationException($"No rebuilder registered for stream type '{streamType}'.");

    public bool IsRegistered(string streamType) => _rebuilders.ContainsKey(streamType);

    public IReadOnlyCollection<string> RegisteredStreamTypes => _rebuilders.Keys;
}
