using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record SendTextMessageBody([property: JsonPropertyName("body")] string Body);
