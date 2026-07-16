using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GymChatAI.Infrastructure.Identity;

/// <summary>
/// Defense-in-depth for multi-tenant isolation: even though every admin endpoint already
/// requires authentication, this filter additionally checks that the {gymId} route value
/// matches the caller's own gym_id claim, so one gym's operator can never query another
/// gym's data just by changing the id in the URL. PlatformAdmin (future SaaS staff) bypasses this.
/// </summary>
public class GymScopeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        if (user.IsPlatformAdmin())
            return await next(context);

        var routeGymId = context.HttpContext.GetRouteValue("gymId")?.ToString();
        var claimGymId = user.GetGymId();

        if (routeGymId is not null && (claimGymId is null || !string.Equals(routeGymId, claimGymId.ToString(), StringComparison.OrdinalIgnoreCase)))
            return Results.Forbid();

        return await next(context);
    }
}