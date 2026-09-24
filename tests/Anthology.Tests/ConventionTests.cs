using System.Reflection;
using Anthology.Kernel;
using Anthology.Modules.Tracking;
using FluentAssertions;
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
    public void All_projections_implement_ResetAsync_so_they_can_be_rebuilt()
    {
        var projectionTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } b
                && b.GetGenericTypeDefinition() == typeof(Deedbox.Projection<>))
            .ToList();

        projectionTypes.Should().NotBeEmpty("there should be projection implementations in the assembly");

        foreach (var projection in projectionTypes)
        {
            projection.GetMethod("ResetAsync", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Should().NotBeNull($"{projection.Name} must override ResetAsync so the admin endpoint can rebuild it");
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
}
