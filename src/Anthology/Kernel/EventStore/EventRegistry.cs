namespace Anthology.Kernel.EventStore;

public sealed class EventRegistry
{
    private readonly Dictionary<Type, string> _byType = new();
    private readonly Dictionary<string, Type> _byName = new();

    public void Map<T>(string eventType) where T : IDomainEvent
    {
        _byType[typeof(T)] = eventType;
        _byName[eventType] = typeof(T);
    }

    public string NameOf(Type type) =>
        _byType.TryGetValue(type, out var name)
            ? name
            : throw new InvalidOperationException($"No event type registered for {type.Name}.");

    public Type TypeOf(string name) =>
        _byName.TryGetValue(name, out var type)
            ? type
            : throw new InvalidOperationException($"No CLR type registered for event type '{name}'.");

    public IEnumerable<Type> RegisteredTypes => _byType.Keys;
}
