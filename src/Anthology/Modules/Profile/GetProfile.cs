using System.Security.Claims;
using Anthology.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Profile;

public static class GetProfile
{
    public sealed record ProfileDto(Guid UserId, string DisplayName);

    public sealed class Handler(ProfileDbContext db)
    {
        public async Task<Result<ProfileDto>> Handle(Guid userId, CancellationToken ct)
        {
            var profile = await db.Profiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);

            return profile is null
                ? Error.NotFound("profile.not_found", "Profile not found.")
                : new ProfileDto(profile.UserId, profile.DisplayName);
        }
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/me", async (
            ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.Handle(user.UserId(), ct)).ToHttpResult())
            .RequireAuthorization();
}
