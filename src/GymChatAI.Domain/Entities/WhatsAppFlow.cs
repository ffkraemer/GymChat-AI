using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// A WhatsApp Flow (Meta's native multi-screen form experience) managed from the
/// Administration Portal - the richer alternative to the button/list-based
/// OnboardingFlowHandler, at the cost of requiring encryption + Business Verification.
/// </summary>
public class WhatsAppFlow : Entity
{
    public Guid GymId { get; private set; }

    public string Name { get; private set; } = default!;

    /// <summary>Meta's own id for this flow, assigned once created via the API.</summary>
    public string? MetaFlowId { get; private set; }

    public WhatsAppFlowStatus Status { get; private set; } = WhatsAppFlowStatus.Draft;

    /// <summary>The Flow JSON (screens/components) currently associated with this flow.</summary>
    public string FlowJson { get; private set; } = "{}";

    private WhatsAppFlow() { }

    public WhatsAppFlow(Guid gymId, string name, string flowJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Flow name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(flowJson))
            throw new ArgumentException("Flow JSON is required.", nameof(flowJson));

        GymId = gymId;
        Name = name;
        FlowJson = flowJson;
    }

    public void MarkCreated(string metaFlowId) => MetaFlowId = metaFlowId;

    public void UpdateFlowJson(string flowJson)
    {
        if (!string.IsNullOrWhiteSpace(flowJson)) FlowJson = flowJson;
        // Meta reverts a published flow to Draft whenever its assets change - mirror that here.
        if (Status == WhatsAppFlowStatus.Published) Status = WhatsAppFlowStatus.Draft;
        Touch();
    }

    public void MarkPublished() => Status = WhatsAppFlowStatus.Published;

    public void MarkDeprecated() => Status = WhatsAppFlowStatus.Deprecated;

    public void SyncStatus(WhatsAppFlowStatus status) => Status = status;
}
