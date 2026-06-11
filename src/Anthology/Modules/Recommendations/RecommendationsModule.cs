using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Recommendations;

public static class RecommendationsModule
{
    public static IServiceCollection AddRecommendationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RecommendationsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
