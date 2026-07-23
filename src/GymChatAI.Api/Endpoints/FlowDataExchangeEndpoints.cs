using GymChatAI.Application.Flows;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace GymChatAI.Api.Endpoints;

public record FlowDataExchangeRequest(string encrypted_flow_data, string encrypted_aes_key, string initial_vector);

/// <summary>
/// The WhatsApp Flows "Data Exchange" endpoint - receives an encrypted request whenever a
/// user's Flow needs dynamic data (in our case, just the "INIT" step, to inject the gym's
/// ClassTypes) or when Meta pings the endpoint's health. See
/// WhatsAppFlowDataExchangeHandler for what each action does, and
/// WhatsAppFlowEncryptionService for the encryption itself.
///
/// Deliberately separate from the regular /webhooks/whatsapp/ endpoint: this one is not
/// JSON in/JSON out - the body is an encrypted envelope, and the response must be a raw
/// base64 string (not wrapped in JSON), per Meta's spec.
/// </summary>
public static class FlowDataExchangeEndpoints
{
    public static IEndpointRouteBuilder MapFlowDataExchangeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/whatsapp/flow-data-exchange", async (
            FlowDataExchangeRequest request,
            WhatsAppFlowEncryptionService encryptionService,
            WhatsAppFlowDataExchangeHandler dataExchangeHandler,
            IOptions<WhatsAppFlowOptions> flowOptions,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var decrypted = encryptionService.DecryptRequest(
                    request.encrypted_flow_data, request.encrypted_aes_key, request.initial_vector,
                    flowOptions.Value.PrivateKeyPem, flowOptions.Value.PrivateKeyPassword);

                var responseJson = await dataExchangeHandler.HandleAsync(decrypted.Json, cancellationToken);
                var encryptedResponse = encryptionService.EncryptResponse(responseJson, decrypted.AesKey, decrypted.RequestIv);

                return Results.Text(encryptedResponse, "text/plain");
            }
            catch (Exception ex)
            {
                // Per Meta's spec: if we can't decrypt/process the request (e.g. a stale or
                // wrong encryption key), respond with 421 so Meta prompts a key refresh,
                // rather than a generic 500 that gives no actionable signal.
                logger.LogError(ex, "Failed to process WhatsApp Flow Data Exchange request.");
                return Results.StatusCode(421);
            }
        })
        .WithTags("WhatsApp Flows")
        .Accepts<FlowDataExchangeRequest>("application/json");

        return app;
    }
}