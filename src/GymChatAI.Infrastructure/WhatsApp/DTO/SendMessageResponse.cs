using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record SendMessageResponse(
    [property: JsonPropertyName("messages")] List<SendMessageResponseItem>? Messages);

internal record SendMessageResponseItem([property: JsonPropertyName("id")] string Id);