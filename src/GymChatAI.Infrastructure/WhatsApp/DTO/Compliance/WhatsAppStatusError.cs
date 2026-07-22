using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

public record WhatsAppStatusError(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("message")] string? Message);