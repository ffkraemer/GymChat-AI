using System.Text.RegularExpressions;
using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// A WhatsApp message template, manageable from the Administration Portal instead of
/// requiring the gym to log into Meta Business Manager directly. Uses the same friendly
/// {VariableName} placeholder syntax as Campaign.MessageTemplate for consistency; these get
/// translated to Meta's positional {{1}}, {{2}}... syntax only at submission time (see
/// WhatsAppTemplateManagementClient in Infrastructure).
/// </summary>
public class WhatsAppMessageTemplate : Entity
{
    private static readonly Regex VariablePattern = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    public Guid GymId { get; private set; }

    /// <summary>Meta requires lowercase letters, numbers, and underscores only.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>BCP-47-ish language code Meta expects, e.g. "pt_PT", "en_US".</summary>
    public string Language { get; private set; } = default!;

    public WhatsAppTemplateCategory Category { get; private set; }

    /// <summary>Body text using {VariableName} placeholders, e.g. "Olá {FirstName}! Bem-vindo ao {GymName}.".</summary>
    public string BodyText { get; private set; } = default!;

    public WhatsAppTemplateStatus Status { get; private set; } = WhatsAppTemplateStatus.Draft;

    /// <summary>Meta's own id for this template, assigned once submitted.</summary>
    public string? MetaTemplateId { get; private set; }

    public string? RejectionReason { get; private set; }

    private WhatsAppMessageTemplate() { }

    public WhatsAppMessageTemplate(Guid gymId, string name, string language, WhatsAppTemplateCategory category, string bodyText)
    {
        if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[a-z0-9_]+$"))
            throw new ArgumentException("Template name must contain only lowercase letters, numbers, and underscores.", nameof(name));
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language is required.", nameof(language));
        if (string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("Body text is required.", nameof(bodyText));

        GymId = gymId;
        Name = name;
        Language = language;
        Category = category;
        BodyText = bodyText;
    }

    /// <summary>The ordered, de-duplicated list of {VariableName} placeholders in the body - position N maps to Meta's {{N}}.</summary>
    public IReadOnlyList<string> ExtractVariableNames() =>
        VariablePattern.Matches(BodyText)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

    public void UpdateDraft(string bodyText, WhatsAppTemplateCategory category)
    {
        if (Status != WhatsAppTemplateStatus.Draft)
            throw new InvalidOperationException("Only draft templates can be edited - once submitted, Meta template bodies are immutable; create a new template instead.");

        if (!string.IsNullOrWhiteSpace(bodyText)) BodyText = bodyText;
        Category = category;
        Touch();
    }

    public void MarkSubmitted(string metaTemplateId)
    {
        MetaTemplateId = metaTemplateId;
        Status = WhatsAppTemplateStatus.PendingApproval;
    }

    public void MarkApproved()
    {
        Status = WhatsAppTemplateStatus.Approved;
        RejectionReason = null;
    }

    public void MarkRejected(string reason)
    {
        Status = WhatsAppTemplateStatus.Rejected;
        RejectionReason = reason;
    }

    public void MarkPaused() => Status = WhatsAppTemplateStatus.Paused;

    public void MarkDisabled() => Status = WhatsAppTemplateStatus.Disabled;

    public void SyncStatus(WhatsAppTemplateStatus status, string? rejectionReason = null)
    {
        Status = status;
        RejectionReason = status == WhatsAppTemplateStatus.Rejected ? rejectionReason : null;
    }
}
