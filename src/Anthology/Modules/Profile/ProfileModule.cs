using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Profile;

public static class ProfileModule
{
    public static IServiceCollection AddProfileModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProfileDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<GetProfile.Handler>();
        services.AddScoped<UpdateProfile.Handler>();

        return services;
    }

    public static WebApplication MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");
        GetProfile.Map(group);
        UpdateProfile.Map(group);
        return app;
    }
}
