using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.Name = "anthology.auth";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });

        return services;
    }

    public static WebApplication MapIdentityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> userManager) =>
        {
            var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);
            return result.Succeeded
                ? Results.Ok(new AuthResponse(user.Id, user.Email!))
                : Results.ValidationProblem(result.Errors.ToDictionary(
                    e => e.Code, e => new[] { e.Description }));
        });

        group.MapPost("/login", async (
            LoginRequest request,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var result = await signInManager.PasswordSignInAsync(
                request.Email, request.Password, isPersistent: true, lockoutOnFailure: false);
            if (!result.Succeeded)
                return TypedResults.Problem("Invalid credentials.", statusCode: 401, title: "auth.invalid_credentials");

            var user = await signInManager.UserManager.FindByEmailAsync(request.Email);
            return Results.Ok(new AuthResponse(user!.Id, user.Email!));
        });

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok();
        }).RequireAuthorization();

        group.MapGet("/me", (HttpContext context) =>
        {
            var user = context.User;
            if (user.Identity?.IsAuthenticated != true)
                return Results.Json(new { authenticated = false }, statusCode: 401);

            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            return Results.Ok(new AuthResponse(Guid.Parse(userId!), email ?? ""));
        });

        return app;
    }

    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record AuthResponse(Guid UserId, string Email);
}
