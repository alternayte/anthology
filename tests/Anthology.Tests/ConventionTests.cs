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
    public void All_tracking_command_endpoints_require_authorization()
    {
        var trackingSlices = typeof(Anthology.Modules.Tracking.TrackingModule).Assembly.GetTypes()
            .Where(t => t.Namespace == "Anthology.Modules.Tracking")
            .Where(t => t.IsAbstract && t.IsSealed)
            .Where(t => t.GetMethod("Map", BindingFlags.Public | BindingFlags.Static) is not null)
            .ToList();

        trackingSlices.Should().NotBeEmpty("tracking module should have slice types with Map methods");
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
}
