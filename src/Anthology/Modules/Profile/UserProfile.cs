using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthology.Modules.Profile;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles", "profile");
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.DisplayName).HasMaxLength(100).IsRequired();
    }
}
