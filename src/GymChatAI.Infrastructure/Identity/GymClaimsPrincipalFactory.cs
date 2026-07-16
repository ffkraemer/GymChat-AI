using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.Identity;

/// <summary>
/// Adds a "gym_id" claim to every signed-in user's principal, so the Api can enforce
/// tenant isolation (see GymScopeFilter) purely from the bearer token, without an extra
/// database lookup on every request.
/// </summary>
public class GymClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>
{
    public const string GymIdClaimType = "gym_id";

    public GymClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(GymIdClaimType, user.GymId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FullName));
        return identity;
    }
}
