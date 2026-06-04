using System.Security.Claims;

namespace Anthology.Kernel;

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id claim."));
}
