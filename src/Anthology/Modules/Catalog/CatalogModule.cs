using Microsoft.EntityFrameworkCore;
using Refit;

namespace Anthology.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        // TMDB
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.Section));
        services.AddTransient<TmdbAuthHandler>();
        services.AddRefitClient<ITmdbApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.themoviedb.org/3"))
            .AddHttpMessageHandler<TmdbAuthHandler>();
        services.AddScoped<ICatalogProvider, TmdbProvider>();

        // Open Library
        services.AddRefitClient<IOpenLibraryApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://openlibrary.org"));
        services.AddScoped<ICatalogProvider, OpenLibraryProvider>();

        // IGDB
        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.Section));
        services.AddTransient<IgdbAuthHandler>();
        services.AddHttpClient<IgdbClient>(c => c.BaseAddress = new Uri("https://api.igdb.com/v4/"))
            .AddHttpMessageHandler<IgdbAuthHandler>();
        services.AddScoped<ICatalogProvider, IgdbProvider>();

        // MusicBrainz
        services.AddTransient<MusicBrainzUserAgentHandler>();
        services.AddRefitClient<IMusicBrainzApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://musicbrainz.org/ws/2"))
            .AddHttpMessageHandler<MusicBrainzUserAgentHandler>();
        services.AddScoped<ICatalogProvider, MusicBrainzProvider>();

        services.AddScoped<SearchTitles.Handler>();
        services.AddScoped<AddTitle.Handler>();
        services.AddScoped<GetTitle.Handler>();

        return services;
    }
}
