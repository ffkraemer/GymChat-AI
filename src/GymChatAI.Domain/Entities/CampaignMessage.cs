using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Records one dispatch of a campaign to one recipient. This is the loyalty engine's
/// idempotency guard: before sending, it checks whether a CampaignMessage already
/// exists for (CampaignId, MemberId, period) so the same member never gets the same
/// welcome/birthday/reactivation message twice. This is also, by nature, an immutable
/// audit log - it's never soft-deleted, only ever added to.
/// </summary>
public class CampaignMessage : Entity
{
    public Guid CampaignId { get; private set; }

    public Guid GymId { get; private set; }

    public Guid? MemberId { get; private set; }

    public string RecipientPhoneNumber { get; private set; } = default!;

    public string RenderedContent { get; private set; } = default!;

    public CampaignMessageStatus Status { get; private set; } = CampaignMessageStatus.Pending;

    /// <summary>
    /// Idempotency key beyond CampaignId+MemberId, e.g. the year for birthday campaigns
    /// (so the same member can receive the campaign again next year) or the check-in
    /// date for reactivation campaigns. Null for one-shot campaigns like Welcome/Manual.
    /// </summary>
    public string? PeriodKey { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    private CampaignMessage() { }

    public CampaignMessage(Guid campaignId, Guid gymId, string recipientPhoneNumber, string renderedContent, Guid? memberId = null, string? periodKey = null)
    {
        if (string.IsNullOrWhiteSpace(recipientPhoneNumber))
            throw new ArgumentException("Recipient phone number is required.", nameof(recipientPhoneNumber));
        if (string.IsNullOrWhiteSpace(renderedContent))
            throw new ArgumentException("Rendered content is required.", nameof(renderedContent));

        CampaignId = campaignId;
        GymId = gymId;
        MemberId = memberId;
        RecipientPhoneNumber = recipientPhoneNumber;
        RenderedContent = renderedContent;
        PeriodKey = periodKey;
    }

    public void MarkSent()
    {
        Status = CampaignMessageStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed() => Status = CampaignMessageStatus.Failed;
}
