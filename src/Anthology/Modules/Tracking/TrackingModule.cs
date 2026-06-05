using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Anthology.Modules.Tracking;

public static class TrackingModule
{
    public static IServiceCollection AddTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TrackingDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
                .UseSnakeCaseNamingConvention());

        services.AddInlineProjection<DiaryProjection>();
        services.AddInlineProjection<LibraryProjection>();

        services.AddScoped<GetDiary.Handler>();
        services.AddScoped<GetLibrary.Handler>();

        return services;
    }

    public static void RegisterEvents(EventRegistry registry)
    {
        registry.Map<ItemWanted>("tracking.item.wanted", currentVersion: 2, upcasters:
        [
            Upcaster.From(1, json =>
            {
                json["titleName"] ??= "Unknown";
                json["mediaType"] ??= "film";
            })
        ]);
        registry.Map<ItemStarted>("tracking.item.started");
        registry.Map<ItemFinished>("tracking.item.finished");
        registry.Map<ItemAbandoned>("tracking.item.abandoned");
        registry.Map<ItemRerated>("tracking.item.rerated");
    }
}
