using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Infrastructure.Identity;

/// <summary>
/// Seeds the Admin/PlatformAdmin roles and two demo accounts (a gym Admin and a
/// PlatformAdmin), so the Administration Portal can be logged into immediately without a
/// separate bootstrap step. Change/remove the demo passwords before any real deployment -
/// they are intentionally simple and printed to the log.
/// </summary>
public static class DemoAdminSeeder
{
    public const string DemoAdminEmail = "admin@demo.gymchat.ai";
    public const string DemoAdminPassword = "GymChat!Demo123";

    public const string DemoPlatformAdminEmail = "platform@demo.gymchat.ai";
    public const string DemoPlatformAdminPassword = "GymChat!Platform123";

    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager, UserManager<ApplicationUser> userManager, Guid gymId, ILogger? logger = null)
    {
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin));

        if (!await roleManager.RoleExistsAsync(Roles.PlatformAdmin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.PlatformAdmin));

        await SeedUserAsync(userManager, DemoAdminEmail, DemoAdminPassword, "Demo Admin", gymId, Roles.Admin, logger);

        // A PlatformAdmin isn't scoped to any single gym - GymId is set to Guid.Empty as a
        // sentinel. GymScopeFilter already bypasses gym-route checks for this role, and
        // gym-management endpoints (create gym, register operator) don't need a "home" gym.
        await SeedUserAsync(userManager, DemoPlatformAdminEmail, DemoPlatformAdminPassword, "Demo Platform Admin", Guid.Empty, Roles.PlatformAdmin, logger);
    }

    private static async Task SeedUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        Guid gymId,
        string role,
        ILogger? logger)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            GymId = gymId,
            FullName = fullName,
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
            logger?.LogInformation(
                "Seeded demo {Role} account: {Email} / {Password} (change this before any real deployment).",
                role, email, password);
        }
        else
        {
            logger?.LogWarning("Failed to seed demo {Role} account: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
