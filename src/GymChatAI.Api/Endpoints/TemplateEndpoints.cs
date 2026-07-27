using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Templates;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateTemplateDraftRequest(string Name, string Language, WhatsAppTemplateCategory Category, string BodyText, Guid? GymId = null);

public record TemplateResponse(
    Guid Id, string Name, string Language, string Category, string? ActualCategory, bool CategoryMismatch, string BodyText,
    string Status, string? MetaTemplateId, string? RejectionReason, IReadOnlyList<string> VariableNames)
{
    public static TemplateResponse From(WhatsAppMessageTemplate template)
    {
        var categoryMismatch = template.ActualCategory is not null
            && !string.Equals(template.ActualCategory, template.Category.ToString(), StringComparison.OrdinalIgnoreCase);

        return new(
            template.Id, template.Name, template.Language, template.Category.ToString(), template.ActualCategory, categoryMismatch, template.BodyText,
            template.Status.ToString(), template.MetaTemplateId, template.RejectionReason, template.ExtractVariableNames());
    }
}

public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/templates").WithTags("WhatsApp Templates");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        // Filtered to the gym's *current* WABA - hides drafts/templates left behind from a
        // WABA the gym has since switched away from (e.g. moving off a test WABA).
        var getByGym = group.MapGet("/{gymId:guid}", async (Guid gymId, WhatsAppTemplateHandler handler, CancellationToken ct) =>
        {
            var templates = await handler.GetVisibleTemplatesAsync(gymId, ct);
            return Results.Ok(templates.Select(TemplateResponse.From));
        });
        if (requireAuth) getByGym.AddEndpointFilter<GymScopeFilter>();

        group.MapPost("/", async (CreateTemplateDraftRequest request, HttpContext httpContext, WhatsAppTemplateHandler handler, CancellationToken ct) =>
        {
            var gymId = requireAuth ? httpContext.User.GetGymId() : request.GymId;
            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            try
            {
                var template = await handler.CreateDraftAsync(gymId.Value, request.Name, request.Language, request.Category, request.BodyText, ct);
                return Results.Created($"/api/templates/{gymId}", TemplateResponse.From(template));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/submit", async (Guid id, IWhatsAppMessageTemplateRepository repository, HttpContext httpContext, WhatsAppTemplateHandler handler, CancellationToken ct) =>
        {
            var template = await repository.GetByIdAsync(id, ct);
            if (template is null) return Results.NotFound();
            if (requireAuth && !IsOwnedByCaller(template, httpContext)) return Results.Forbid();

            try
            {
                await handler.SubmitForApprovalAsync(id, ct);
                var updated = await repository.GetByIdAsync(id, ct);
                return Results.Ok(TemplateResponse.From(updated!));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, IWhatsAppMessageTemplateRepository repository, HttpContext httpContext, WhatsAppTemplateHandler handler, CancellationToken ct) =>
        {
            var template = await repository.GetByIdAsync(id, ct);
            if (template is null) return Results.NotFound();
            if (requireAuth && !IsOwnedByCaller(template, httpContext)) return Results.Forbid();

            try
            {
                await handler.DeleteDraftAsync(id, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var refreshStatuses = group.MapPost("/{gymId:guid}/refresh-statuses", async (Guid gymId, WhatsAppTemplateHandler handler, CancellationToken ct) =>
        {
            await handler.RefreshStatusesAsync(gymId, ct);
            var templates = await handler.GetVisibleTemplatesAsync(gymId, ct);
            return Results.Ok(templates.Select(TemplateResponse.From));
        });
        if (requireAuth) refreshStatuses.AddEndpointFilter<GymScopeFilter>();

        return app;
    }

    private static bool IsOwnedByCaller(WhatsAppMessageTemplate template, HttpContext httpContext)
    {
        if (httpContext.User.IsPlatformAdmin()) return true;
        return template.GymId == httpContext.User.GetGymId();
    }
}
