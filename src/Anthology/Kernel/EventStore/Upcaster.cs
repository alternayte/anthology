using System.Text.Json.Nodes;

namespace Anthology.Kernel.EventStore;

public sealed record Upcaster(int FromVersion, Action<JsonNode> Transform)
{
    public static Upcaster From(int version, Action<JsonNode> transform) => new(version, transform);
}

public sealed record EventResolution(Type ClrType, IReadOnlyList<Upcaster> Upcasters);
