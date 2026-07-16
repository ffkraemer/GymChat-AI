using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Represents a loyalty campaign: a message template plus a triggering rule.
/// Welcome/Birthday/Reactivation campaigns are evaluated automatically by the
/// loyalty engine; Manual campaigns are dispatched on demand from the Administration Portal.
/// </summary>
public class Campaign : Entity
{
    public Guid GymId { get; private set; }

    public string Name { get; private set; } = default!;

    public CampaignType Type { get; private set; }

    /// <summary>
    /// The message body sent to recipients. Supports simple placeholders such as
    /// {FirstName} and {GymName}, resolved by the Application layer at send time.
    /// </summary>
    public string MessageTemplate { get; private set; } = default!;

    /// <summary>
    /// Meaning depends on <see cref="Type"/>:
    /// Welcome -> days after joining; Reactivation -> days of inactivity threshold.
    /// Unused for Birthday and Manual campaigns.
    /// </summary>
    public int? TriggerDayOffset { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Campaign() { }

    public Campaign(Guid gymId, string name, CampaignType type, string messageTemplate, int? triggerDayOffset = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Campaign name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(messageTemplate))
            throw new ArgumentException("Message template is required.", nameof(messageTemplate));
        if ((type == CampaignType.Welcome || type == CampaignType.Reactivation) && triggerDayOffset is null)
            throw new ArgumentException($"{type} campaigns require a trigger day offset.", nameof(triggerDayOffset));

        GymId = gymId;
        Name = name;
        Type = type;
        MessageTemplate = messageTemplate;
        TriggerDayOffset = triggerDayOffset;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateTemplate(string messageTemplate)
    {
        if (!string.IsNullOrWhiteSpace(messageTemplate))
            MessageTemplate = messageTemplate;
        Touch();
    }
}
