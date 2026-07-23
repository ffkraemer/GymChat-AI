using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Flows;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateFlowRequestBody(string Name, Guid? GymId = null);

public record FlowResponse(Guid Id, string Name, string? MetaFlowId, string Status)
{
    public static FlowResponse From(WhatsAppFlow flow) => new(flow.Id, flow.Name, flow.MetaFlowId, flow.Status.ToString());
}

public record RegisterFlowEncryptionKeyRequest(string PublicKeyPem);

public record TriggerFlowRequest(string RecipientPhoneNumber, string BodyText, string FlowCtaButtonText);

public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/flows").WithTags("WhatsApp Flows");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var getByGym = group.MapGet("/{gymId:guid}", async (Guid gymId, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flows = await repository.GetAllByGymAsync(gymId, ct);
            return Results.Ok(flows.Select(FlowResponse.From));
        });
        if (requireAuth) getByGym.AddEndpointFilter<GymScopeFilter>();

        // Creates the Flow on Meta's side (with our default preferences Flow JSON) and saves it locally.
        group.MapPost("/", async (CreateFlowRequestBody request, HttpContext httpContext, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            var gymId = requireAuth ? httpContext.User.GetGymId() : request.GymId;
            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            try
            {
                var flowJson = PreferencesFlowJsonBuilder.Build();
                var flow = await handler.CreateAsync(gymId.Value, request.Name, flowJson, ["SIGN_UP"], ct);
                return Results.Created($"/api/flows/{gymId}", FlowResponse.From(flow));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/publish", async (Guid id, HttpContext httpContext, WhatsAppFlowHandler handler, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var existing = await repository.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && existing.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            try
            {
                await handler.PublishAsync(id, ct);
                var flow = await repository.GetByIdAsync(id, ct);
                return flow is null ? Results.NotFound() : Results.Ok(FlowResponse.From(flow));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var refreshStatus = group.MapPost("/{gymId:guid}/refresh-statuses", async (Guid gymId, WhatsAppFlowHandler handler, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flows = await repository.GetAllByGymAsync(gymId, ct);
            foreach (var flow in flows)
                await handler.RefreshStatusAsync(flow.Id, ct);

            var updated = await repository.GetAllByGymAsync(gymId, ct);
            return Results.Ok(updated.Select(FlowResponse.From));
        });
        if (requireAuth) refreshStatus.AddEndpointFilter<GymScopeFilter>();

        // One-time per-WABA setup: registers our RSA public key so Meta can encrypt Data Exchange requests to us.
        var registerKey = group.MapPost("/{gymId:guid}/encryption-key", async (
            Guid gymId, RegisterFlowEncryptionKeyRequest request, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            try
            {
                var success = await handler.RegisterEncryptionKeyAsync(gymId, request.PublicKeyPem, ct);
                return Results.Ok(new { success });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
        if (requireAuth) registerKey.AddEndpointFilter<GymScopeFilter>();

        // Sends the Flow-trigger message to a test recipient - useful to try it out before rolling it out broadly.
        group.MapPost("/{id:guid}/trigger", async (
            Guid id,
            TriggerFlowRequest request,
            HttpContext httpContext,
            IWhatsAppFlowRepository flowRepository,
            IGymRepository gymRepository,
            IWhatsAppFlowTokenStore tokenStore,
            IWhatsAppMessageSender messageSender,
            CancellationToken ct) =>
        {
            var flow = await flowRepository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();
            if (flow.MetaFlowId is null) return Results.BadRequest(new { error = "This flow hasn't been created on Meta's side yet." });

            var gym = await gymRepository.GetByIdAsync(flow.GymId, ct);
            if (gym is null) return Results.NotFound();

            var flowToken = tokenStore.CreateToken(gym.Id, request.RecipientPhoneNumber);
            var wamid = await messageSender.SendFlowMessageAsync(
                gym.WhatsAppPhoneNumberId, request.RecipientPhoneNumber, request.BodyText, request.FlowCtaButtonText,
                flow.MetaFlowId, flowToken, PreferencesFlowJsonBuilder.ScreenId, ct);

            return Results.Ok(new { whatsAppMessageId = wamid });
        });

        return app;
    }
}
