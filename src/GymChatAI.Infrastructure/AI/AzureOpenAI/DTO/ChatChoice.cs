using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);
