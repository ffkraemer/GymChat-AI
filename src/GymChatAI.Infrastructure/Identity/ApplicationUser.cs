using Microsoft.AspNetCore.Identity;

namespace GymChatAI.Infrastructure.Identity;

/// <summary>
/// An operator/administrator account for the Administration Portal. Every user belongs to
/// exactly one gym (GymId) - this is the tenant-scoping mechanism that keeps one gym's staff
/// from seeing another gym's data, enforced via a "gym_id" claim (see GymClaimsPrincipalFactory)
/// and GymScopeFilter on the Api endpoints.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid GymId { get; set; }

    public string FullName { get; set; } = default!;
}

/// <summary>Role names used across the Administration Portal.</summary>
public static class Roles
{
    /// <summary>Manages a single gym: FAQs, plans, promotions, campaigns, other operators.</summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Reserved for the future SaaS phase: cross-tenant access for GymChat AI's own staff.
    /// Not assigned to anyone yet, but GymScopeFilter already knows to bypass gym-scoping for it.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";
}

/// <summary>Authorization policy names used across the Api's endpoint groups.</summary>
public static class Policies
{
    /// <summary>Any authenticated gym Admin (or a future PlatformAdmin) - the baseline for every Administration Portal endpoint.</summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Platform-level operations only (creating gyms, registering the first operator of a
    /// new gym). Deliberately stricter than <see cref="Admin"/> - a gym's own Admin should
    /// never be able to create or manage other gyms.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";
}
