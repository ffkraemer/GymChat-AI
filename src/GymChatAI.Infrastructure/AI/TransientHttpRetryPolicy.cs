using System.Net;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Infrastructure.AI;

/// <summary>
/// Retries transient failures (rate limiting, upstream service hiccups) shared across every
/// AI provider (Gemini, OpenAI, Azure OpenAI) - these are exactly the kind of failure that
/// resolves itself within seconds, so it's worth a couple of automatic attempts before
/// surfacing an error and escalating the conversation to a human.
/// </summary>
public static class TransientHttpRetryPolicy
{
    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
    [
        HttpStatusCode.TooManyRequests,   // 429 - rate limited (common on free tiers)
        HttpStatusCode.ServiceUnavailable, // 503 - upstream temporarily overloaded
        HttpStatusCode.BadGateway,         // 502
        HttpStatusCode.GatewayTimeout      // 504
    ];

    /// <summary>
    /// Calls <paramref name="sendAsync"/> up to <paramref name="maxAttempts"/> times, with
    /// exponential backoff (1s, 2s, 4s, ...) between retryable failures. Returns the last
    /// response either way - callers keep their existing success/failure handling
    /// (IsSuccessStatusCode checks, EnsureSuccessStatusCode, etc.) unchanged.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        ILogger logger,
        string providerName,
        CancellationToken cancellationToken,
        int maxAttempts = 3)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            response?.Dispose();
            response = await sendAsync(cancellationToken);

            var isLastAttempt = attempt == maxAttempts;
            if (response.IsSuccessStatusCode || !RetryableStatusCodes.Contains(response.StatusCode) || isLastAttempt)
                return response;

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s...

            logger.LogWarning(
                "{Provider} returned {StatusCode} (attempt {Attempt}/{MaxAttempts}) - retrying in {DelaySeconds}s.",
                providerName, (int)response.StatusCode, attempt, maxAttempts, delay.TotalSeconds);

            await Task.Delay(delay, cancellationToken);
        }

        return response!;
    }
}
