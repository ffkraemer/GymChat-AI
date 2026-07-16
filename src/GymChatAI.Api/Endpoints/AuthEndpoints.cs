using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CurrentUserResponse(string Email, string FullName, Guid GymId, IReadOnlyList<string> Roles);

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
}