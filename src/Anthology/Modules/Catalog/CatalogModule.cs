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

        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.Section));
        services.AddTransient<TmdbAuthHandler>();
        services.AddRefitClient<ITmdbApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.themoviedb.org/3"))
            .AddHttpMessageHandler<TmdbAuthHandler>();

        services.AddScoped<SearchTitles.Handler>();
        services.AddScoped<AddTitle.Handler>();
        services.AddScoped<GetTitle.Handler>();

        return services;
    }

}
