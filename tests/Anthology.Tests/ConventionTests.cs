using System.Reflection;
using Anthology.Kernel;
using Anthology.Modules.Tracking;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public class ConventionTests
{
    [Fact]
    public void All_command_handlers_are_registered_as_decorated()
    {
        var handlerTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)))
            .Where(t => !t.IsGenericTypeDefinition) // exclude open-generic decorator
            .ToList();

        handlerTypes.Should().NotBeEmpty("there should be command handlers in the assembly");

        foreach (var handler in handlerTypes)
        {
            handler.IsNested.Should().BeTrue(
                $"{handler.FullName} should be a nested type inside its vertical slice");
        }
    }

    [Fact]
    public void All_tracking_endpoints_are_centralized()
    {
        var endpointsType = typeof(Anthology.Modules.Tracking.TrackingEndpoints);
        var mapMethod = endpointsType.GetMethod("MapTrackingEndpoints", BindingFlags.Public | BindingFlags.Static);

        mapMethod.Should().NotBeNull("TrackingEndpoints should have a MapTrackingEndpoints method");
    }

    [Fact]
    public void All_projections_are_registered_via_AddInlineProjection_or_AddAsyncProjection()
    {
        var projectionTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Contains(typeof(Anthology.Kernel.Messaging.IProjection)))
            .ToList();

        projectionTypes.Should().NotBeEmpty("there should be projection implementations in the assembly");

        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddTrackingModule(config);

        var inlineRegistry = services
            .FirstOrDefault(d => d.ServiceType == typeof(Anthology.Kernel.Messaging.InlineProjectionRegistry))
            ?.ImplementationInstance as Anthology.Kernel.Messaging.InlineProjectionRegistry;
        var asyncRegistry = services
            .FirstOrDefault(d => d.ServiceType == typeof(Anthology.Kernel.Messaging.AsyncProjectionRegistry))
            ?.ImplementationInstance as Anthology.Kernel.Messaging.AsyncProjectionRegistry;

        var registeredTypes = new List<Type>();
        if (inlineRegistry is not null) registeredTypes.AddRange(inlineRegistry.ProjectionTypes);
        if (asyncRegistry is not null) registeredTypes.AddRange(asyncRegistry.ProjectionTypes);

        foreach (var projection in projectionTypes)
        {
            registeredTypes.Should().Contain(projection,
                $"{projection.Name} must be registered via AddInlineProjection or AddAsyncProjection");
        }
    }

    [Fact]
    public void All_projections_implement_IRebuildableProjection()
    {
        var projectionTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Contains(typeof(Anthology.Kernel.Messaging.IProjection)))
            .ToList();

        projectionTypes.Should().NotBeEmpty("there should be projection implementations in the assembly");

        foreach (var projection in projectionTypes)
        {
            projection.GetInterfaces().Should().Contain(
                typeof(Anthology.Kernel.Messaging.IRebuildableProjection),
                $"{projection.Name} must implement IRebuildableProjection so it can be rebuilt via admin endpoint");
        }
    }

    [Fact]
    public void Every_command_has_a_handler()
    {
        var commandInterface = typeof(ICommand<>);
        var handlerInterface = typeof(ICommandHandler<,>);

        var commands = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == commandInterface))
            .ToList();

        var handledCommandTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => i.GetGenericArguments()[0]))
            .ToHashSet();

        commands.Should().NotBeEmpty("there should be command types in the assembly");

        foreach (var cmd in commands)
        {
            handledCommandTypes.Should().Contain(cmd,
                $"{cmd.DeclaringType?.Name}.{cmd.Name} has no ICommandHandler<,> implementation");
        }
    }

    [Fact]
    public void All_aggregate_states_have_registered_evolvers()
    {
        var stateTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateState<>)))
            .ToList();

        stateTypes.Should().NotBeEmpty("there should be aggregate state types in the assembly");

        var eventRegistry = new Anthology.Kernel.EventStore.EventRegistry();
        TrackingModule.RegisterEvents(eventRegistry);
        var serializer = new Anthology.Kernel.EventStore.EventSerializer(eventRegistry);
        var evolverRegistry = new Anthology.Kernel.EventStore.StreamEvolverRegistry();
        TrackingModule.RegisterEvolvers(evolverRegistry, serializer);

        foreach (var stateType in stateTypes)
        {
            var streamTypeProp = stateType.GetProperty("StreamType",
                BindingFlags.Public | BindingFlags.Static);
            streamTypeProp.Should().NotBeNull(
                $"{stateType.Name} should have a static StreamType property");

            var streamType = (string)streamTypeProp!.GetValue(null)!;
            evolverRegistry.IsRegistered(streamType).Should().BeTrue(
                $"{stateType.Name} (stream type '{streamType}') must have a registered evolver in its module's RegisterEvolvers method");
        }
    }
}
