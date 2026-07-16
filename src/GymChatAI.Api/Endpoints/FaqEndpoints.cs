using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateFaqRequest(string Question, string Answer, string? Category, Guid? GymId = null);

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
            var faqs = await repository.GetActiveByGymAsync(gymId, ct);
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

        return app;
    }
}