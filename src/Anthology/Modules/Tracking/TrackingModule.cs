using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Tracking;

public static class TrackingModule
{
    public static IServiceCollection AddTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TrackingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IProjection, DiaryProjection>();
        services.AddScoped<IProjection, LibraryProjection>();

        services.AddScoped<GetDiary.Handler>();
        services.AddScoped<GetLibrary.Handler>();

        return services;
    }

    public static WebApplication MapTrackingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tracking").WithTags("Tracking");
        WantItem.Map(group);
        StartItem.Map(group);
        FinishItem.Map(group);
        AbandonItem.Map(group);
        RerateItem.Map(group);
        GetDiary.Map(group);
        GetLibrary.Map(group);
        return app;
    }

    public static void RegisterEvents(EventRegistry registry)
    {
        registry.Map<ItemWanted>("tracking.item.wanted.v1");
        registry.Map<ItemStarted>("tracking.item.started.v1");
        registry.Map<ItemFinished>("tracking.item.finished.v1");
        registry.Map<ItemAbandoned>("tracking.item.abandoned.v1");
        registry.Map<ItemRerated>("tracking.item.rerated.v1");
    }
}
