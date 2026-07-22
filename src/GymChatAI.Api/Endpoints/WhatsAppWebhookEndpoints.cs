using System.Text.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Messaging;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.WhatsApp;
using GymChatAI.Infrastructure.WhatsApp.Mapper;
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
            IGymRepository gymRepository,
            IWhatsAppDeliveryFailureRepository deliveryFailureRepository,
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

            // Deliberately CancellationToken.None here, not the request's own token: message
            // processing (in particular, the AI call) must run to completion regardless of
            // what happens to the inbound HTTP connection from Meta/ngrok. Tying it to
            // HttpContext.RequestAborted caused in-flight AI calls to be killed mid-request
            // whenever that connection hiccuped or Meta's own webhook timeout kicked in -
            // even though the call itself was perfectly healthy.
            foreach (var message in incomingMessages)
            {
                var result = await handler.HandleAsync(message, CancellationToken.None);
                if (!result.IsSuccess)
                    logger.LogWarning("Failed to process message {WhatsAppMessageId}: {Error}", message.WhatsAppMessageId, result.Error);
            }

            // Delivery failures Meta reports after the fact (see the Compliance Dashboard's
            // "Falhas registadas na Meta" section) - a message we successfully sent earlier
            // can still fail to actually reach the recipient.
            var deliveryFailures = WhatsAppWebhookMapper.ExtractDeliveryFailures(payload);
            foreach (var failureEvent in deliveryFailures)
            {
                var gym = await gymRepository.GetByWhatsAppPhoneNumberIdAsync(failureEvent.WhatsAppPhoneNumberId, CancellationToken.None);
                if (gym is null)
                {
                    logger.LogWarning("Delivery failure reported for unknown WhatsApp phone number id {PhoneNumberId}.", failureEvent.WhatsAppPhoneNumberId);
                    continue;
                }

                var failure = new WhatsAppDeliveryFailure(
                    gym.Id, failureEvent.WhatsAppMessageId, failureEvent.RecipientPhoneNumber,
                    failureEvent.ErrorCode, failureEvent.ErrorMessage);

                await deliveryFailureRepository.AddAsync(failure, CancellationToken.None);
                logger.LogWarning(
                    "Meta reported a delivery failure for message {WhatsAppMessageId} to {RecipientPhoneNumber}: {ErrorMessage}",
                    failureEvent.WhatsAppMessageId, failureEvent.RecipientPhoneNumber, failureEvent.ErrorMessage);
            }

            return Results.Ok();
        });

        return app;
    }
}