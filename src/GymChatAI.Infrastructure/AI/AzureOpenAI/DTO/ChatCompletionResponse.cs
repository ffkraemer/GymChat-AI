using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record ChatCompletionResponse(
    [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);
