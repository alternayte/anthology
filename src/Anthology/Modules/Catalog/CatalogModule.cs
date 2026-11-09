using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
            .ConfigureHttpClient((sp, c) =>
                c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<TmdbOptions>>().Value.BaseUrl))
            .AddHttpMessageHandler<TmdbAuthHandler>();
        services.AddScoped<TmdbProvider>();
        services.AddScoped<ICatalogProvider>(sp => sp.GetRequiredService<TmdbProvider>());
        services.AddScoped<ISeedableProvider>(sp => sp.GetRequiredService<TmdbProvider>());

        // Open Library
        services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.Section));
        services.AddRefitClient<IOpenLibraryApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://openlibrary.org"));
        services.AddScoped<OpenLibraryProvider>();
        services.AddScoped<ICatalogProvider>(sp => sp.GetRequiredService<OpenLibraryProvider>());
        services.AddScoped<ISeedableProvider>(sp => sp.GetRequiredService<OpenLibraryProvider>());

        // IGDB
        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.Section));
        services.AddTransient<IgdbAuthHandler>();
        services.AddHttpClient<IgdbClient>((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<IgdbOptions>>().Value;
                c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.BaseUrl)
                    ? IgdbOptions.DefaultBaseUrl
                    : options.BaseUrl);
            })
            .AddHttpMessageHandler<IgdbAuthHandler>();
        services.AddScoped<IgdbProvider>();
        services.AddScoped<ICatalogProvider>(sp => sp.GetRequiredService<IgdbProvider>());
        services.AddScoped<ISeedableProvider>(sp => sp.GetRequiredService<IgdbProvider>());

        // MusicBrainz
        services.Configure<MusicBrainzOptions>(configuration.GetSection(MusicBrainzOptions.Section));
        services.AddTransient<MusicBrainzUserAgentHandler>();
        services.AddRefitClient<IMusicBrainzApi>()
            .ConfigureHttpClient((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<MusicBrainzOptions>>().Value;
                c.BaseAddress = MusicBrainzOptions.ResolveBaseUrl(options.BaseUrl);
            })
            .AddHttpMessageHandler<MusicBrainzUserAgentHandler>();
        services.AddScoped<ICatalogProvider, MusicBrainzProvider>();

        services.AddScoped<SearchTitles.Handler>();
        services.AddScoped<SearchLocal.Handler>();
        services.AddScoped<AddTitle.Handler>();
        services.AddScoped<GetTitle.Handler>();
        services.AddScoped<FindSimilarTitles>();
        services.AddScoped<GetSimilar.Handler>();
        services.AddScoped<GetCreatorTitles.Handler>();
        services.AddScoped<CatalogSeeder>();

        return services;
    }
}
