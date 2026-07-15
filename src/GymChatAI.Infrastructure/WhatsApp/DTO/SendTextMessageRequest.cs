using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

// ---- Outbound (send message) ----

internal record SendTextMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] SendTextMessageBody Text)
{
    public static SendTextMessageRequest Create(string to, string body) =>
        new("whatsapp", to, "text", new SendTextMessageBody(body));
}
