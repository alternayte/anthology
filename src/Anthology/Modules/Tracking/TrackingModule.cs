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
        services.AddInlineProjection<ListProjection>();
        services.AddAsyncProjection<DiaryProjection>();
        services.AddAsyncProjection<LibraryProjection>();
        services.AddAsyncProjection<ListProjection>();

        services.AddScoped<GetDiary.Handler>();
        services.AddScoped<GetLibrary.Handler>();
        services.AddScoped<GetList.Handler>();
        services.AddScoped<GetUserLists.Handler>();

        return services;
    }

    public static void RegisterEvolvers(StreamEvolverRegistry registry, EventSerializer serializer)
    {
        registry.Register<TrackedItemState>(serializer, TrackedItem.Evolve);
        registry.Register<CuratedListState>(serializer, CuratedList.Evolve);
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
        registry.Map<ItemRated>("tracking.item.rated");
        registry.Map<ItemRated>("tracking.item.rerated");

        registry.Map<ListCreated>("tracking.list.created");
        registry.Map<ListRenamed>("tracking.list.renamed");
        registry.Map<ListDescriptionChanged>("tracking.list.description_changed");
        registry.Map<ListVisibilityChanged>("tracking.list.visibility_changed");
        registry.Map<ListDeleted>("tracking.list.deleted");
        registry.Map<ItemAddedToList>("tracking.list.item_added");
        registry.Map<ItemRemovedFromList>("tracking.list.item_removed");
        registry.Map<ListItemReordered>("tracking.list.item_reordered");
    }
}
