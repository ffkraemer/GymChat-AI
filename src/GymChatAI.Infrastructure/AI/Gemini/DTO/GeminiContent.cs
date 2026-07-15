using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record GeminiContent(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("parts")] List<GeminiPart> Parts);

internal record GeminiPart([property: JsonPropertyName("text")] string Text);