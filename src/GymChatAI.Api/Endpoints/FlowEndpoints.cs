using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Flows;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateFlowRequestBody(string Name, Guid? GymId = null);

public record FlowResponse(Guid Id, string Name, string? MetaFlowId, string Status, int ScreenCount)
{
    public static FlowResponse From(WhatsAppFlow flow) => new(flow.Id, flow.Name, flow.MetaFlowId, flow.Status.ToString(), flow.Screens.Count);
}

public record RegisterFlowEncryptionKeyRequest(string PublicKeyPem);

public record SetFlowEndpointRequest(string EndpointUri);

public record TriggerFlowRequest(string RecipientPhoneNumber, string BodyText, string FlowCtaButtonText);

public record ComponentResponse(
    string Type, string Label, string? VariableName, bool Required,
    string? OptionsSource, string? StaticOptionsJson,
    string? FooterAction, string? FooterNextScreenId, string? FooterButtonLabel)
{
    public static ComponentResponse From(FlowComponent c) => new(
        c.Type.ToString(), c.Label, c.VariableName, c.Required,
        c.OptionsSource?.ToString(), c.StaticOptionsJson,
        c.FooterAction?.ToString(), c.FooterNextScreenId, c.FooterButtonLabel);
}

public record ScreenResponse(string ScreenId, string Title, IReadOnlyList<ComponentResponse> Components)
{
    public static ScreenResponse From(FlowScreen s) => new(
        s.ScreenId, s.Title, s.Components.OrderBy(c => c.Order).Select(ComponentResponse.From).ToList());
}

public record ReplaceScreensRequest(IReadOnlyList<ScreenDefinition> Screens);

public record UpdateFlowJsonRequest(string FlowJson);

public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/flows").WithTags("WhatsApp Flows");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var getByGym = group.MapGet("/{gymId:guid}", async (Guid gymId, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            var flows = await handler.GetVisibleFlowsAsync(gymId, ct);
            return Results.Ok(flows.Select(FlowResponse.From));
        });
        if (requireAuth) getByGym.AddEndpointFilter<GymScopeFilter>();

        // Raw JSON editing (an alternative to the structured screens editor above) - lets an
        // admin hand-edit or upload a Flow JSON directly, closer to Meta's own tool.
        group.MapGet("/{id:guid}/json", async (Guid id, HttpContext httpContext, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            return Results.Ok(new { flowJson = flow.FlowJson });
        });

        group.MapPut("/{id:guid}/json", async (
            Guid id, UpdateFlowJsonRequest request, HttpContext httpContext, WhatsAppFlowHandler handler, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            try
            {
                System.Text.Json.JsonDocument.Parse(request.FlowJson);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Results.BadRequest(new { error = $"JSON inválido: {ex.Message}" });
            }

            try
            {
                var validationErrors = await handler.UpdateFlowJsonAsync(id, request.FlowJson, ct);
                return Results.Ok(new { validationErrors });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Loads a flow's current screen graph, for the Flow Designer to edit.
        group.MapGet("/{id:guid}/screens", async (Guid id, HttpContext httpContext, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            var screens = flow.Screens.OrderBy(s => s.Order).Select(ScreenResponse.From).ToList();
            return Results.Ok(screens);
        });

        // Saves the Flow Designer's screen graph - always the complete set, not incremental diffs.
        group.MapPost("/{id:guid}/screens", async (
            Guid id, ReplaceScreensRequest request, HttpContext httpContext, WhatsAppFlowHandler handler, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            try
            {
                var validationErrors = await handler.ReplaceScreensAsync(id, request.Screens, ct);
                return Results.Ok(new { validationErrors });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Creates the Flow on Meta's side (with our default preferences Flow JSON) and saves it locally.
        group.MapPost("/", async (CreateFlowRequestBody request, HttpContext httpContext, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            var gymId = requireAuth ? httpContext.User.GetGymId() : request.GymId;
            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            try
            {
                var flow = await handler.CreateAsync(gymId.Value, request.Name, ["SIGN_UP"], ct);
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

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IWhatsAppFlowRepository repository, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

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

        var refreshStatus = group.MapPost("/{gymId:guid}/refresh-statuses", async (Guid gymId, WhatsAppFlowHandler handler, CancellationToken ct) =>
        {
            var flows = await handler.GetVisibleFlowsAsync(gymId, ct);
            foreach (var flow in flows)
                await handler.RefreshStatusAsync(flow.Id, ct);

            var updated = await handler.GetVisibleFlowsAsync(gymId, ct);
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

        // Tells Meta where our Data Exchange endpoint lives for this Flow - required before
        // publishing a dynamic Flow (ours declares data_api_version, since class types are
        // gym-specific). The URL changes whenever the ngrok tunnel restarts in development,
        // so this is a separate, repeatable action rather than a one-time setup step.
        var setEndpoint = group.MapPost("/{id:guid}/endpoint", async (
            Guid id, SetFlowEndpointRequest request, HttpContext httpContext, WhatsAppFlowHandler handler, IWhatsAppFlowRepository repository, CancellationToken ct) =>
        {
            var flow = await repository.GetByIdAsync(id, ct);
            if (flow is null) return Results.NotFound();
            if (requireAuth && !httpContext.User.IsPlatformAdmin() && flow.GymId != httpContext.User.GetGymId())
                return Results.Forbid();

            try
            {
                var success = await handler.SetFlowEndpointAsync(id, request.EndpointUri, ct);
                return Results.Ok(new { success });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

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

            var firstScreen = flow.Screens.OrderBy(s => s.Order).FirstOrDefault();
            if (firstScreen is null) return Results.BadRequest(new { error = "This flow has no screens defined yet." });

            var flowToken = tokenStore.CreateToken(gym.Id, request.RecipientPhoneNumber, flow.Id);
            var wamid = await messageSender.SendFlowMessageAsync(
                gym.WhatsAppPhoneNumberId, request.RecipientPhoneNumber, request.BodyText, request.FlowCtaButtonText,
                flow.MetaFlowId, flowToken, firstScreen.ScreenId, ct);

            return Results.Ok(new { whatsAppMessageId = wamid });
        });

        return app;
    }
}
