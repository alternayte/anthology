using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Profile;

public static class ProfileModule
{
    public static IServiceCollection AddProfileModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProfileDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<GetProfile.Handler>();
        services.AddScoped<UpdateProfile.Handler>();

        return services;
    }
}
