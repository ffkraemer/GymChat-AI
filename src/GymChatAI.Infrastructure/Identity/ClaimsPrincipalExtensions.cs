using System.Security.Claims;

namespace GymChatAI.Infrastructure.Identity;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the authenticated operator's gym id from their bearer token claims.</summary>
    public static Guid? GetGymId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(GymClaimsPrincipalFactory.GymIdClaimType)?.Value;
        return Guid.TryParse(value, out var gymId) ? gymId : null;
    }

    public static bool IsPlatformAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.PlatformAdmin);
}
