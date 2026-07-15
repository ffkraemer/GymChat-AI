using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Api.Endpoints;

public record CreateFaqRequest(Guid GymId, string Question, string Answer, string? Category);

public record FaqResponse(Guid Id, string Question, string Answer, string? Category, bool IsActive)
{
    public static FaqResponse From(Faq faq) => new(faq.Id, faq.Question, faq.Answer, faq.Category, faq.IsActive);
}

public static class FaqEndpoints
{
    public static IEndpointRouteBuilder MapFaqEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/faqs").WithTags("FAQs");

        group.MapGet("/{gymId:guid}", async (Guid gymId, IFaqRepository repository, CancellationToken ct) =>
        {
            var faqs = await repository.GetActiveByGymAsync(gymId, ct);
            return Results.Ok(faqs.Select(FaqResponse.From));
        });

        group.MapPost("/", async (CreateFaqRequest request, IFaqRepository repository, CancellationToken ct) =>
        {
            var faq = new Faq(request.GymId, request.Question, request.Answer, request.Category);
            await repository.AddAsync(faq, ct);
            return Results.Created($"/api/faqs/{faq.GymId}", FaqResponse.From(faq));
        });

        return app;
    }
}
