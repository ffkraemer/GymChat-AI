namespace GymChatAI.Application.Abstractions;

/// <summary>
/// Handles the "missing link" step Meta's newer UI stopped doing automatically: telling a
/// WABA which App should receive its webhook events (POST /{waba-id}/subscribed_apps).
/// Without this, incoming WhatsApp messages never reach our webhook, no matter how correctly
/// everything else (Callback URL, Verify Token) is configured.
/// </summary>
public interface IWhatsAppWabaAdminClient
{
    /// <summary>Subscribes our App to receive webhook events from this WABA. Returns false (not an exception) on failure, so callers can degrade gracefully.</summary>
    Task<bool> SubscribeAppToWabaAsync(string whatsAppBusinessAccountId, CancellationToken cancellationToken = default);
}
