using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
