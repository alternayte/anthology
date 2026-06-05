using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public sealed record RegistryTestEvent(string Value) : IDomainEvent;

public sealed record RegistryTestState(string Latest, int EventCount) : IAggregateState<RegistryTestState>
{
    public static RegistryTestState Initial => new("", 0);
    public static string StreamType => "registry_test";
}

public sealed class StreamEvolverRegistryTests
{
    private static RegistryTestState Evolve(RegistryTestState s, IDomainEvent e) => e switch
    {
        RegistryTestEvent t => s with { Latest = t.Value, EventCount = s.EventCount + 1 },
        _ => s
    };

    private (StreamEvolverRegistry Registry, EventRegistry EventRegistry, EventSerializer Serializer) CreateRegistry()
    {
        var eventRegistry = new EventRegistry();
        eventRegistry.Map<RegistryTestEvent>("registry.test.event");
        var serializer = new EventSerializer(eventRegistry);
        var registry = new StreamEvolverRegistry();
        registry.Register<RegistryTestState>(serializer, Evolve);
        return (registry, eventRegistry, serializer);
    }

    [Fact]
    public void Registered_stream_type_is_discoverable()
    {
        var (registry, _, _) = CreateRegistry();

        registry.IsRegistered("registry_test").Should().BeTrue();
        registry.RegisteredStreamTypes.Should().Contain("registry_test");
    }

    [Fact]
    public void Unregistered_stream_type_is_not_discoverable()
    {
        var (registry, _, _) = CreateRegistry();

        registry.IsRegistered("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void GetRebuilder_for_unregistered_type_throws()
    {
        var (registry, _, _) = CreateRegistry();

        var act = () => registry.GetRebuilder("nonexistent");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public void Rebuilder_folds_events_into_correct_state()
    {
        var (registry, eventRegistry, serializer) = CreateRegistry();
        var rebuilder = registry.GetRebuilder("registry_test");
        var eventTypeName = eventRegistry.NameOf(typeof(RegistryTestEvent));

        var events = new List<EventRow>
        {
            new() { EventType = eventTypeName, Payload = serializer.Serialize(new RegistryTestEvent("first")) },
            new() { EventType = eventTypeName, Payload = serializer.Serialize(new RegistryTestEvent("second")) },
        };

        var stateJson = rebuilder(events);
        var state = serializer.DeserializeState<RegistryTestState>(stateJson);

        state.Latest.Should().Be("second");
        state.EventCount.Should().Be(2);
    }

    [Fact]
    public void Rebuilder_returns_initial_state_for_empty_event_list()
    {
        var (registry, _, serializer) = CreateRegistry();
        var rebuilder = registry.GetRebuilder("registry_test");

        var stateJson = rebuilder([]);
        var state = serializer.DeserializeState<RegistryTestState>(stateJson);

        state.Should().Be(RegistryTestState.Initial);
    }
}
