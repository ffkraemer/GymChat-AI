using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Infrastructure.Identity;

/// <summary>
/// Seeds the Admin role and one demo operator account, so the Administration Portal can be
/// logged into immediately without a separate bootstrap step. Change/remove the demo
/// password before any real deployment - it is intentionally simple and printed to the log.
/// </summary>
public static class DemoAdminSeeder
{
    public const string DemoAdminEmail = "admin@demo.gymchat.ai";
    public const string DemoAdminPassword = "GymChat!Demo123";

    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager, UserManager<ApplicationUser> userManager, Guid gymId, ILogger? logger = null)
    {
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin));

        if (!await roleManager.RoleExistsAsync(Roles.PlatformAdmin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.PlatformAdmin));

        var existingAdmin = await userManager.FindByEmailAsync(DemoAdminEmail);
        if (existingAdmin is not null) return;

        var admin = new ApplicationUser
        {
            UserName = DemoAdminEmail,
            Email = DemoAdminEmail,
            EmailConfirmed = true,
            GymId = gymId,
            FullName = "Demo Admin",
        };

        var result = await userManager.CreateAsync(admin, DemoAdminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
            logger?.LogInformation(
                "Seeded demo admin account: {Email} / {Password} (change this before any real deployment).",
                DemoAdminEmail, DemoAdminPassword);
        }
        else
        {
            logger?.LogWarning("Failed to seed demo admin account: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
