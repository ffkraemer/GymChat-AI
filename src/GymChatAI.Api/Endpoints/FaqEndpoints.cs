using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateFaqRequest(string Question, string Answer, string? Category, Guid? GymId = null);

public record UpdateFaqRequest(string Question, string Answer, string? Category);

public record FaqResponse(Guid Id, string Question, string Answer, string? Category, bool IsActive)
{
    public static FaqResponse From(Faq faq) => new(faq.Id, faq.Question, faq.Answer, faq.Category, faq.IsActive);
}

public static class FaqEndpoints
{
    public static IEndpointRouteBuilder MapFaqEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/faqs").WithTags("FAQs");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var getByGym = group.MapGet("/{gymId:guid}", async (Guid gymId, IFaqRepository repository, CancellationToken ct) =>
        {
            var faqs = await repository.GetAllByGymAsync(gymId, ct);
            return Results.Ok(faqs.Select(FaqResponse.From));
        });
        if (requireAuth) getByGym.AddEndpointFilter<GymScopeFilter>();

        // When auth is enabled, GymId always comes from the operator's own claim - never
        // from the request body - so one gym's admin can never create a FAQ for another gym.
        // When auth is disabled (in-memory mode has no user store to authenticate against),
        // the request body's GymId is used instead.
        group.MapPost("/", async (CreateFaqRequest request, HttpContext httpContext, IFaqRepository repository, CancellationToken ct) =>
        {
            var gymId = requireAuth ? httpContext.User.GetGymId() : request.GymId;
            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            var faq = new Faq(gymId.Value, request.Question, request.Answer, request.Category);
            await repository.AddAsync(faq, ct);
            return Results.Created($"/api/faqs/{faq.GymId}", FaqResponse.From(faq));
        });

        // Edit an existing FAQ's content.
        group.MapPut("/{id:guid}", async (Guid id, UpdateFaqRequest request, HttpContext httpContext, IFaqRepository repository, CancellationToken ct) =>
        {
            var faq = await repository.GetByIdAsync(id, ct);
            if (faq is null) return Results.NotFound();

            if (requireAuth && !IsOwnedByCaller(faq, httpContext)) return Results.Forbid();

            faq.Update(request.Question, request.Answer, request.Category);
            await repository.UpdateAsync(faq, ct);
            return Results.Ok(FaqResponse.From(faq));
        });

        // Soft-delete: FAQs are never hard-deleted, only deactivated (so the AI stops using
        // them for grounding, but the history/audit trail stays intact).
        group.MapPost("/{id:guid}/deactivate", async (Guid id, HttpContext httpContext, IFaqRepository repository, CancellationToken ct) =>
        {
            var faq = await repository.GetByIdAsync(id, ct);
            if (faq is null) return Results.NotFound();

            if (requireAuth && !IsOwnedByCaller(faq, httpContext)) return Results.Forbid();

            faq.Deactivate();
            await repository.UpdateAsync(faq, ct);
            return Results.Ok(FaqResponse.From(faq));
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, HttpContext httpContext, IFaqRepository repository, CancellationToken ct) =>
        {
            var faq = await repository.GetByIdAsync(id, ct);
            if (faq is null) return Results.NotFound();

            if (requireAuth && !IsOwnedByCaller(faq, httpContext)) return Results.Forbid();

            faq.Activate();
            await repository.UpdateAsync(faq, ct);
            return Results.Ok(FaqResponse.From(faq));
        });

        return app;
    }

    // These three routes are keyed by FAQ id, not by {gymId} in the path, so GymScopeFilter
    // (which only checks a route value literally named "gymId") doesn't apply automatically -
    // check ownership manually instead, the same way CampaignEndpoints does for /trigger.
    private static bool IsOwnedByCaller(Faq faq, HttpContext httpContext)
    {
        if (httpContext.User.IsPlatformAdmin()) return true;
        return faq.GymId == httpContext.User.GetGymId();
    }
}
