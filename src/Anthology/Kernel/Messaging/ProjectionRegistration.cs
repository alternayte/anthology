namespace Anthology.Kernel.Messaging;

public sealed class InlineProjectionRegistry
{
    private readonly List<Type> _types = [];
    public IReadOnlyList<Type> ProjectionTypes => _types;
    internal void Add<T>() where T : class, IProjection => _types.Add(typeof(T));
}

public sealed class AsyncProjectionRegistry
{
    private readonly List<Type> _types = [];
    public IReadOnlyList<Type> ProjectionTypes => _types;
    public IReadOnlyList<string> ProjectionNames => _types.Select(t => t.Name).ToList();
    internal void Add<T>() where T : class, IProjection => _types.Add(typeof(T));
}

public static class ProjectionRegistrationExtensions
{
    public static IServiceCollection AddInlineProjection<T>(this IServiceCollection services)
        where T : class, IProjection
    {
        services.AddScoped<T>();

        var registry = services
            .FirstOrDefault(d => d.ServiceType == typeof(InlineProjectionRegistry))
            ?.ImplementationInstance as InlineProjectionRegistry;

        if (registry is null)
        {
            registry = new InlineProjectionRegistry();
            services.AddSingleton(registry);
        }

        registry.Add<T>();
        return services;
    }

    public static IServiceCollection AddAsyncProjection<T>(this IServiceCollection services)
        where T : class, IProjection
    {
        services.AddScoped<T>();

        var registry = services
            .FirstOrDefault(d => d.ServiceType == typeof(AsyncProjectionRegistry))
            ?.ImplementationInstance as AsyncProjectionRegistry;

        if (registry is null)
        {
            registry = new AsyncProjectionRegistry();
            services.AddSingleton(registry);
        }

        registry.Add<T>();
        return services;
    }
}
