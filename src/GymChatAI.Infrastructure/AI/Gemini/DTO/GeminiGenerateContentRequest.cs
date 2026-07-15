using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.AI;

internal record GeminiSystemInstruction([property: JsonPropertyName("parts")] List<GeminiPart> Parts);

internal record GeminiGenerationConfig(
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
    [property: JsonPropertyName("thinkingConfig")] GeminiThinkingConfig ThinkingConfig);

internal record GeminiGenerateContentRequest(
    [property: JsonPropertyName("contents")] List<GeminiContent> Contents,
    [property: JsonPropertyName("systemInstruction")] GeminiSystemInstruction SystemInstruction,
    [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

internal record GeminiThinkingConfig([property: JsonPropertyName("thinkingLevel")] string ThinkingLevel);