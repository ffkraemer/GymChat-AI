using System.Text.Json;
using GymChatAI.Application.Messaging;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace GymChatAI.Api.Endpoints;

public static class WhatsAppWebhookEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks/whatsapp").WithTags("WhatsApp Webhook");

        // Meta calls this once, synchronously, when you configure the webhook URL in the App Dashboard.
        // https://developers.facebook.com/docs/graph-api/webhooks/getting-started#verification-requests
        group.MapGet("/", (HttpRequest request, IOptions<WhatsAppOptions> options) =>
        {
            var mode = request.Query["hub.mode"].FirstOrDefault();
            var verifyToken = request.Query["hub.verify_token"].FirstOrDefault();
            var challenge = request.Query["hub.challenge"].FirstOrDefault();

            if (mode == "subscribe" && verifyToken == options.Value.WebhookVerifyToken && challenge is not null)
                return Results.Text(challenge, "text/plain");

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        // Meta calls this for every event (messages, statuses, etc.).
        group.MapPost("/", async (
            HttpRequest request,
            ProcessIncomingMessageHandler handler,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            WhatsAppWebhookPayload? payload;
            try
            {
                payload = await JsonSerializer.DeserializeAsync<WhatsAppWebhookPayload>(request.Body, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse WhatsApp webhook payload.");
                // Still return 200 so Meta doesn't retry a payload we'll never be able to parse.
                return Results.Ok();
            }

            if (payload is null)
                return Results.Ok();

            var incomingMessages = WhatsAppWebhookMapper.ExtractIncomingMessages(payload);

            // WhatsApp expects a fast 200 OK; for the POC we process inline, but this is the
            // natural seam to push onto a background queue (e.g. Azure Service Bus) in the MVP phase.
            foreach (var message in incomingMessages)
            {
                var result = await handler.HandleAsync(message, cancellationToken);
                if (!result.IsSuccess)
                    logger.LogWarning("Failed to process message {WhatsAppMessageId}: {Error}", message.WhatsAppMessageId, result.Error);
            }

            return Results.Ok();
        });

        return app;
    }
}
