using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Compliance;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/compliance").WithTags("Compliance");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var getSnapshot = group.MapGet("/{gymId:guid}", async (
            Guid gymId,
            IGymRepository gymRepository,
            ComplianceDashboardHandler handler,
            CancellationToken ct) =>
        {
            var gym = await gymRepository.GetByIdAsync(gymId, ct);
            if (gym is null) return Results.NotFound();

            var snapshot = await handler.GetSnapshotAsync(gym, ct);
            return Results.Ok(snapshot);
        });
        if (requireAuth) getSnapshot.AddEndpointFilter<GymScopeFilter>();

        return app;
    }
}
