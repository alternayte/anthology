using Deedbox;
using Npgsql;

namespace Anthology.Modules.Tracking;

public static class TrackingModule
{
    public static IServiceCollection AddTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TrackingDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
                .UseSnakeCaseNamingConvention());

        services.AddScoped<GetDiary.Handler>();
        services.AddScoped<GetLibrary.Handler>();
        services.AddScoped<GetList.Handler>();
        services.AddScoped<GetUserLists.Handler>();

        return services;
    }

    public static IReadOnlyList<string> StreamTypes { get; } = [TrackedItemState.StreamType, CuratedListState.StreamType];

    /// <summary>The stored names predate Deedbox, so every stream and event name is explicit.</summary>
    public static DeedboxBuilder AddTracking(this DeedboxBuilder deedbox) => deedbox
        .Stream<TrackedItemState>(TrackedItemState.StreamType, s => s
            .Event<ItemWanted>(2, e => e
                .Name("tracking.item.wanted")
                .From(1, json =>
                {
                    json["titleName"] ??= "Unknown";
                    json["mediaType"] ??= "film";
                }))
            .Event<ItemStarted>("tracking.item.started")
            .Event<ItemFinished>("tracking.item.finished")
            .Event<ItemAbandoned>("tracking.item.abandoned")
            .Event<ItemRated>(e => e.Name("tracking.item.rated").Alias("tracking.item.rerated")))
        .Stream<CuratedListState>(CuratedListState.StreamType, s => s
            .Event<ListCreated>("tracking.list.created")
            .Event<ListRenamed>("tracking.list.renamed")
            .Event<ListDescriptionChanged>("tracking.list.description_changed")
            .Event<ListVisibilityChanged>("tracking.list.visibility_changed")
            .Event<ListDeleted>("tracking.list.deleted")
            .Event<ItemAddedToList>("tracking.list.item_added")
            .Event<ItemRemovedFromList>("tracking.list.item_removed")
            .Event<ListItemReordered>("tracking.list.item_reordered"))
        .Projection<DiaryProjection>("diary", Run.Inline)
        .Projection<LibraryProjection>("library", Run.Inline)
        .Projection<ListProjection>("lists", Run.Inline);
}
