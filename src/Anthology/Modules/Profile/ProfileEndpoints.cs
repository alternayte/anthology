using System.Security.Claims;
using Anthology.Kernel;
using FluentValidation;

namespace Anthology.Modules.Profile;

public static class ProfileEndpoints
{
    public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfile.Command>
    {
        public UpdateProfileValidator()
        {
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        }
    }

    public static WebApplication MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/me", async (
            ClaimsPrincipal user, GetProfile.Handler handler, CancellationToken ct) =>
            (await handler.Handle(user.UserId(), ct)).ToHttpResult())
            .RequireAuthorization();

        group.MapPut("/me", async (
            UpdateProfile.Command command, ClaimsPrincipal user, UpdateProfile.Handler handler, CancellationToken ct) =>
            (await handler.Handle(user.UserId(), command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<UpdateProfile.Command>>()
            .RequireAuthorization();

        return app;
    }
}
