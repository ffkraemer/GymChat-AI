using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

// ---- Inbound (webhook payload) ----
// Reference: https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/payload-examples

public record WhatsAppWebhookPayload(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] List<WhatsAppEntry>? Entry);

public record WhatsAppEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("changes")] List<WhatsAppChange>? Changes);