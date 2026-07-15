using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record ChatCompletionRequest(
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens);
