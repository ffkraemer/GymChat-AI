using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Infrastructure.LanguageDetection;

/// <summary>
/// Simple keyword/stop-word based language detector. Good enough for the POC's
/// three target languages without adding an external NLP dependency or extra
/// AI round-trip. Can be swapped for Azure AI Language later without touching
/// the Application layer, since it only depends on ILanguageDetector.
/// </summary>
public class HeuristicLanguageDetector : ILanguageDetector
{
    private static readonly string[] PortugueseMarkers =
        { " o ", " a ", " de ", " que ", " para ", " com ", " ola", " obrigado", " obrigada", " ginasio", " academia", " sim", " nao" };

    private static readonly string[] SpanishMarkers =
        { " el ", " la ", " de ", " que ", " para ", " con ", " hola", " gracias", " gimnasio", " si ", " no " };

    private static readonly string[] EnglishMarkers =
        { " the ", " a ", " of ", " that ", " for ", " with ", " hi ", " hello", " thanks", " gym", " yes", " no " };

    public Task<Language> DetectAsync(string text, CancellationToken cancellationToken = default)
    {
        var normalized = " " + text.ToLowerInvariant() + " ";

        var scores = new (Language Language, int Score)[]
        {
            (Language.Portuguese, CountMatches(normalized, PortugueseMarkers)),
            (Language.Spanish, CountMatches(normalized, SpanishMarkers)),
            (Language.English, CountMatches(normalized, EnglishMarkers)),
        };

        var best = scores.OrderByDescending(s => s.Score).First();

        // Default to Portuguese (the platform's primary market) when no strong signal is found,
        // rather than surfacing "Unknown" to the assistant.
        var result = best.Score > 0 ? best.Language : Language.Portuguese;
        return Task.FromResult(result);
    }

    private static int CountMatches(string normalizedText, string[] markers) =>
        markers.Count(marker => normalizedText.Contains(marker, StringComparison.Ordinal));
}
