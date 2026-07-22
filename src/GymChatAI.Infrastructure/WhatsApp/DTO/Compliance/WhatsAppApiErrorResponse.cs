using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record WhatsAppApiErrorResponse([property: JsonPropertyName("error")] WhatsAppApiErrorDetail? Error);

internal record WhatsAppApiErrorDetail(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("code")] int? Code);