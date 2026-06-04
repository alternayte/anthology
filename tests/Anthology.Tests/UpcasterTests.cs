using System.Text.Json.Nodes;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public class UpcasterTests
{
    [Fact]
    public void Registry_resolves_current_version_without_upcasters()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event");

        var resolution = registry.Resolve("test.event.v1");

        resolution.ClrType.Should().Be(typeof(TestEvent));
        resolution.Upcasters.Should().BeEmpty();
    }

    [Fact]
    public void Registry_resolves_old_version_with_upcaster_chain()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json => json["extra"] = "added")
        ]);

        var resolution = registry.Resolve("test.event.v1");

        resolution.ClrType.Should().Be(typeof(TestEvent));
        resolution.Upcasters.Should().HaveCount(1);
        resolution.Upcasters[0].FromVersion.Should().Be(1);
    }

    [Fact]
    public void Registry_resolves_current_version_with_no_upcasters_when_old_versions_exist()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json => json["extra"] = "added")
        ]);

        var resolution = registry.Resolve("test.event.v2");

        resolution.Upcasters.Should().BeEmpty();
    }

    [Fact]
    public void NameOf_returns_current_versioned_name()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 3, upcasters:
        [
            Upcaster.From(1, _ => { }),
            Upcaster.From(2, _ => { })
        ]);

        registry.NameOf(typeof(TestEvent)).Should().Be("test.event.v3");
    }

    [Fact]
    public void Multi_version_chain_runs_all_upcasters_from_stored_version()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 3, upcasters:
        [
            Upcaster.From(1, json => json["step1"] = true),
            Upcaster.From(2, json => json["step2"] = true)
        ]);

        var v1Resolution = registry.Resolve("test.event.v1");
        v1Resolution.Upcasters.Should().HaveCount(2);

        var v2Resolution = registry.Resolve("test.event.v2");
        v2Resolution.Upcasters.Should().HaveCount(1);
        v2Resolution.Upcasters[0].FromVersion.Should().Be(2);
    }

    [Fact]
    public void Serializer_applies_upcaster_during_deserialization()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json => json["value"] = "upcasted")
        ]);
        var serializer = new EventSerializer(registry);

        var v1Payload = """{"value":"original"}""";
        var result = serializer.Deserialize("test.event.v1", v1Payload);

        result.Should().BeOfType<TestEvent>().Which.Value.Should().Be("upcasted");
    }

    [Fact]
    public void Serializer_skips_upcasting_for_current_version()
    {
        var registry = new EventRegistry();
        registry.Map<TestEvent>("test.event", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json => json["value"] = "upcasted")
        ]);
        var serializer = new EventSerializer(registry);

        var v2Payload = """{"value":"current"}""";
        var result = serializer.Deserialize("test.event.v2", v2Payload);

        result.Should().BeOfType<TestEvent>().Which.Value.Should().Be("current");
    }

    [Fact]
    public void Resolve_throws_for_unknown_event_type()
    {
        var registry = new EventRegistry();

        var act = () => registry.Resolve("nonexistent.v1");

        act.Should().Throw<InvalidOperationException>();
    }
}
