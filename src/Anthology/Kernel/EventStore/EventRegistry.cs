namespace Anthology.Kernel.EventStore;

public sealed class EventRegistry
{
    private readonly Dictionary<Type, string> _byType = new();
    private readonly Dictionary<string, EventResolution> _byName = new();

    public void Map<T>(string baseEventType) where T : IDomainEvent =>
        Map<T>(baseEventType, 1);

    public void Map<T>(string baseEventType, int currentVersion, params Upcaster[] upcasters) where T : IDomainEvent
    {
        var currentName = $"{baseEventType}.v{currentVersion}";
        _byType[typeof(T)] = currentName;
        _byName[currentName] = new EventResolution(typeof(T), []);

        foreach (var upcaster in upcasters.OrderBy(u => u.FromVersion))
        {
            var oldName = $"{baseEventType}.v{upcaster.FromVersion}";
            var chain = upcasters
                .Where(u => u.FromVersion >= upcaster.FromVersion)
                .OrderBy(u => u.FromVersion)
                .ToList();
            _byName[oldName] = new EventResolution(typeof(T), chain);
        }
    }

    public string NameOf(Type type) =>
        _byType.TryGetValue(type, out var name)
            ? name
            : throw new InvalidOperationException($"No event type registered for {type.Name}.");

    public EventResolution Resolve(string storedEventType) =>
        _byName.TryGetValue(storedEventType, out var resolution)
            ? resolution
            : throw new InvalidOperationException($"No CLR type registered for event type '{storedEventType}'.");

    public IEnumerable<Type> RegisteredTypes => _byType.Keys;
}
