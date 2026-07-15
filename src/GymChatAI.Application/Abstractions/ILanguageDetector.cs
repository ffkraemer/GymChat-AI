using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Abstractions;

/// <summary>Port for detecting the language of an inbound message.</summary>
public interface ILanguageDetector
{
    Task<Language> DetectAsync(string text, CancellationToken cancellationToken = default);
}
