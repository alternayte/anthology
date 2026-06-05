using System.Security.Claims;
using Anthology.Kernel;

namespace Anthology.Modules.Profile;

public static class ProfileEndpoints
{
    public static WebApplication MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/me", async (
            ClaimsPrincipal user, GetProfile.Handler handler, CancellationToken ct) =>
            (await handler.Handle(user.UserId(), ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapPut("/me", async (
            UpdateProfile.Command command,
            ClaimsPrincipal user,
            ICommandHandler<UpdateProfile.Command, Result<UpdateProfile.ProfileDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(command with { UserId = user.UserId() }, ct)).ToHttpResult())
            .RequireAuthorization();

        return app;
    }
}
