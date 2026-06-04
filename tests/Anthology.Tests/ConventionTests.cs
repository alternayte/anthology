using System.Reflection;
using Anthology.Kernel;
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
    public void All_tracking_command_endpoints_require_authorization()
    {
        var trackingSlices = typeof(Anthology.Modules.Tracking.TrackingModule).Assembly.GetTypes()
            .Where(t => t.Namespace == "Anthology.Modules.Tracking")
            .Where(t => t.IsAbstract && t.IsSealed)
            .Where(t => t.GetMethod("Map", BindingFlags.Public | BindingFlags.Static) is not null)
            .ToList();

        trackingSlices.Should().NotBeEmpty("tracking module should have slice types with Map methods");
    }
}
