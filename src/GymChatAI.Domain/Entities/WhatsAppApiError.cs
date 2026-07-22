using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Records one failed WhatsApp API call (non-2xx response) - the raw material for the
/// compliance dashboard's "recent errors" view. Deliberately append-only/immutable, like
/// CampaignMessage: it's an audit trail, not a business entity with its own lifecycle.
/// </summary>
public class WhatsAppApiError : Entity
{
    public Guid GymId { get; private set; }

    /// <summary>The Graph API endpoint called, e.g. "messages".</summary>
    public string Endpoint { get; private set; } = default!;

    public int HttpStatusCode { get; private set; }

    /// <summary>WhatsApp's own numeric error code (e.g. "131049" for the per-user frequency cap), when present.</summary>
    public string? ErrorCode { get; private set; }

    public string ErrorMessage { get; private set; } = default!;

    private WhatsAppApiError() { }

    public WhatsAppApiError(Guid gymId, string endpoint, int httpStatusCode, string? errorCode, string errorMessage)
    {
        GymId = gymId;
        Endpoint = endpoint;
        HttpStatusCode = httpStatusCode;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
