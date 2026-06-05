using System.Text.Json.Serialization;
using Anthology.Kernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Anthology.Modules.Profile;

public static class UpdateProfile
{
    public sealed record UpdateProfileCommand(string DisplayName, [property: JsonIgnore] Guid UserId = default)
        : ICommand<Result<ProfileDto>>;

    public sealed class Validator : AbstractValidator<UpdateProfileCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        }
    }

    public sealed record ProfileDto(Guid UserId, string DisplayName);

    public sealed class Handler(ProfileDbContext db)
        : ICommandHandler<UpdateProfileCommand, Result<ProfileDto>>
    {
        public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
        {
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == command.UserId, ct);

            if (profile is null)
            {
                profile = new UserProfile { UserId = command.UserId, DisplayName = command.DisplayName };
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
}
