using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace GymChatAI.Api.Endpoints;

public record CurrentUserResponse(string Email, string FullName, Guid GymId, IReadOnlyList<string> Roles);

public record RegisterOperatorRequest(string Email, string Password, string FullName, Guid GymId);

public record RegisterOperatorResponse(Guid Id, string Email, string FullName, Guid GymId);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapCurrentUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", (HttpContext httpContext) =>
        {
            var user = httpContext.User;
            var gymId = user.GetGymId();
            if (gymId is null) return Results.Forbid();

            var email = user.Identity?.Name ?? string.Empty;
            var fullName = user.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? email;
            var roles = user.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            return Results.Ok(new CurrentUserResponse(email, fullName, gymId.Value, roles));
        })
        .RequireAuthorization()
        .WithTags("Auth");

        return app;
    }

    /// <summary>
    /// Creates the first (or an additional) Admin operator account for a gym. Restricted to
    /// PlatformAdmin: a gym's own Admin should never be able to create accounts for other
    /// gyms, so this is deliberately a platform-level operation, not something exposed to
    /// regular gym operators.
    /// </summary>
    public static IEndpointRouteBuilder MapRegisterOperatorEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register-operator", async (
            RegisterOperatorRequest request,
            UserManager<ApplicationUser> userManager,
            IGymRepository gymRepository,
            CancellationToken ct) =>
        {
            var gym = await gymRepository.GetByIdAsync(request.GymId, ct);
            if (gym is null)
                return Results.BadRequest(new { error = "Gym not found." });

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                GymId = request.GymId,
                FullName = request.FullName,
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await userManager.AddToRoleAsync(user, Roles.Admin);

            return Results.Created(
                $"/api/gyms/{gym.WhatsAppPhoneNumberId}",
                new RegisterOperatorResponse(user.Id, user.Email!, user.FullName, user.GymId));
        })
        .RequireAuthorization(Policies.PlatformAdmin)
        .WithTags("Auth");

        return app;
    }
}
