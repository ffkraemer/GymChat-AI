namespace GymChatAI.Application.Abstractions;

public record WhatsAppTemplateSubmissionResult(string MetaTemplateId);

/// <summary>One template's current status as reported by Meta, keyed by MetaTemplateId.</summary>
public record WhatsAppTemplateRemoteStatus(string MetaTemplateId, string Status, string? Category, string? RejectionReason);

/// <summary>
/// Port for managing WhatsApp message templates directly from our Portal, instead of
/// requiring the gym to use Meta Business Manager. Separate from IWhatsAppMessageSender
/// (sending) and IWhatsAppComplianceClient (health) - this is template lifecycle management.
/// </summary>
public interface IWhatsAppTemplateManagementClient
{
    /// <summary>Submits a new template to Meta for review. Returns Meta's own id for it.</summary>
    Task<WhatsAppTemplateSubmissionResult> SubmitTemplateAsync(
        string whatsAppBusinessAccountId,
        string name,
        string language,
        string category,
        string bodyText,
        IReadOnlyList<string> variableNames,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the current review status (and Meta's own category, which can differ from what was submitted) of every template under this WABA.</summary>
    Task<IReadOnlyList<WhatsAppTemplateRemoteStatus>> GetTemplateStatusesAsync(
        string whatsAppBusinessAccountId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a template on Meta's side by name (all language versions). Returns false (not an exception) on failure so callers can decide how to handle it.</summary>
    Task<bool> DeleteTemplateAsync(string whatsAppBusinessAccountId, string templateName, CancellationToken cancellationToken = default);
}
