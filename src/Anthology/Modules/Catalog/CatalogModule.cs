using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Refit;

namespace Anthology.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Embedding
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.Section));
        services.AddHttpClient("EmbeddingApi", (sp, client) =>
        {
            var opts = configuration.GetSection(EmbeddingOptions.Section).Get<EmbeddingOptions>()!;
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", opts.ApiKey);
        });


        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                    o => o.UseVector())
                .UseSnakeCaseNamingConvention());

        // TMDB
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.Section));
        services.AddTransient<TmdbAuthHandler>();
        services.AddRefitClient<ITmdbApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.themoviedb.org/3"))
            .AddHttpMessageHandler<TmdbAuthHandler>();
        services.AddScoped<ICatalogProvider, TmdbProvider>();
        services.AddScoped<ISeedableProvider, TmdbProvider>();

        // Open Library
        services.AddRefitClient<IOpenLibraryApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://openlibrary.org"));
        services.AddScoped<ICatalogProvider, OpenLibraryProvider>();
        services.AddScoped<ISeedableProvider, OpenLibraryProvider>();

        // IGDB
        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.Section));
        services.AddTransient<IgdbAuthHandler>();
        services.AddHttpClient<IgdbClient>(c => c.BaseAddress = new Uri("https://api.igdb.com/v4/"))
            .AddHttpMessageHandler<IgdbAuthHandler>();
        services.AddScoped<ICatalogProvider, IgdbProvider>();
        services.AddScoped<ISeedableProvider, IgdbProvider>();

        // MusicBrainz
        services.AddTransient<MusicBrainzUserAgentHandler>();
        services.AddRefitClient<IMusicBrainzApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://musicbrainz.org/ws/2"))
            .AddHttpMessageHandler<MusicBrainzUserAgentHandler>();
        services.AddScoped<ICatalogProvider, MusicBrainzProvider>();

        services.AddScoped<SearchTitles.Handler>();
        services.AddScoped<SearchLocal.Handler>();
        services.AddScoped<AddTitle.Handler>();
        services.AddScoped<GetTitle.Handler>();
        services.AddScoped<GetSimilar.Handler>();
        services.AddScoped<GetCreatorTitles.Handler>();
        services.AddScoped<CatalogSeeder>();

        return services;
    }
}
