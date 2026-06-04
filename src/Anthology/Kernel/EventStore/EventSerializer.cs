using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Anthology.Kernel.EventStore;

public sealed class EventSerializer
{
    private readonly EventRegistry _registry;
    private readonly JsonSerializerOptions _options;

    public EventSerializer(EventRegistry registry)
    {
        _registry = registry;
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }

    public string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _options);

    public IDomainEvent Deserialize(string eventType, string payload)
    {
        var clrType = _registry.TypeOf(eventType);
        return (IDomainEvent)(JsonSerializer.Deserialize(payload, clrType, _options)
            ?? throw new InvalidOperationException($"Failed to deserialize event of type '{eventType}'."));
    }

    public string SerializeMetadata(EventMetadata metadata) =>
        JsonSerializer.Serialize(metadata, _options);

    public EventMetadata DeserializeMetadata(string json) =>
        JsonSerializer.Deserialize<EventMetadata>(json, _options)
            ?? throw new InvalidOperationException("Failed to deserialize event metadata.");
}
