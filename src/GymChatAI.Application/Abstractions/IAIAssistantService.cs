using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Abstractions;

/// <summary>A single turn of conversational context fed to the AI assistant.</summary>
public record AIConversationTurn(string Role, string Content);

/// <summary>Grounding data made available to the AI assistant when composing a reply.</summary>
public record AIAssistantContext(
    string GymName,
    Language PreferredLanguage,
    IReadOnlyList<AIConversationTurn> History,
    IReadOnlyList<(string Question, string Answer)> RelevantFaqs);

/// <summary>
/// Port for the generative AI assistant (Azure OpenAI in this POC).
/// Application only depends on this abstraction, never on a concrete SDK/HTTP client.
/// </summary>
public interface IAIAssistantService
{
    Task<string> GenerateReplyAsync(AIAssistantContext context, string userMessage, CancellationToken cancellationToken = default);
}