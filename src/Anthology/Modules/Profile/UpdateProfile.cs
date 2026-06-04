using System.Security.Claims;
using Anthology.Kernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Profile;

public static class UpdateProfile
{
    public sealed record Command(string DisplayName);

    public sealed record ProfileDto(Guid UserId, string DisplayName);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        }
    }

    public sealed class Handler(ProfileDbContext db)
    {
        public async Task<Result<ProfileDto>> Handle(Guid userId, Command command, CancellationToken ct)
        {
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

            if (profile is null)
            {
                profile = new UserProfile { UserId = userId, DisplayName = command.DisplayName };
                db.Profiles.Add(profile);
            }
            else
            {
                profile.DisplayName = command.DisplayName;
            }

            await db.SaveChangesAsync(ct);
            return new ProfileDto(profile.UserId, profile.DisplayName);
        }
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/me", async (
            Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.Handle(user.UserId(), command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<Command>>()
            .RequireAuthorization();
}
