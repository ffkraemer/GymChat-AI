using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record GeminiCandidate([property: JsonPropertyName("content")] GeminiContent? Content);

internal record GeminiGenerateContentResponse(
    [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates);