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
            .RequireAuthorization().WithName("getProfile").Produces<GetProfile.ProfileDto>();

        group.MapPut("/me", async (
            UpdateProfile.UpdateProfileCommand command,
            ClaimsPrincipal user,
            ICommandHandler<UpdateProfile.UpdateProfileCommand, Result<UpdateProfile.ProfileDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(command with { UserId = user.UserId() }, ct)).ToHttpResult())
            .RequireAuthorization().WithName("updateProfile").Produces<UpdateProfile.ProfileDto>();

        return app;
    }
}
