using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Represents a WhatsApp conversation with a contact (a lead or a member).
/// This is the aggregate root for the messaging bounded context: messages
/// are only ever added through the conversation, keeping ordering and
/// status transitions consistent.
/// </summary>
public class Conversation : Entity
{
    private readonly List<Message> _messages = new();

    public Guid GymId { get; private set; }

    public string ContactPhoneNumber { get; private set; } = default!;

    public Guid? LeadId { get; private set; }

    public Guid? MemberId { get; private set; }

    public ConversationStatus Status { get; private set; } = ConversationStatus.Open;

    public Language PreferredLanguage { get; private set; } = Language.Unknown;

    public DateTimeOffset LastMessageAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Where this conversation is in the guided WhatsApp menu (onboarding/preferences).
    /// None means "ordinary conversation" - inbound messages go to the AI assistant as usual.
    /// Anything else means the next inbound message is expected to be a button/list reply
    /// for this step, and is handled by OnboardingFlowHandler instead.
    /// </summary>
    public ConversationFlowStep FlowStep { get; private set; } = ConversationFlowStep.None;

    /// <summary>
    /// Small scratch value for mid-flow state that doesn't deserve its own table - e.g. "the
    /// day the member just picked, while we wait for them to pick the time window to pair it
    /// with". Always cleared once the step that needed it completes.
    /// </summary>
    public string? PendingFlowData { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public Conversation(Guid gymId, string contactPhoneNumber, Guid? leadId = null, Guid? memberId = null)
    {
        if (string.IsNullOrWhiteSpace(contactPhoneNumber))
            throw new ArgumentException("Contact phone number is required.", nameof(contactPhoneNumber));

        GymId = gymId;
        ContactPhoneNumber = contactPhoneNumber;
        LeadId = leadId;
        MemberId = memberId;
    }

    public Message AddInboundMessage(string content, string? whatsAppMessageId, Language detectedLanguage)
    {
        var message = new Message(Id, MessageDirection.Inbound, MessageOrigin.Human, content, whatsAppMessageId, detectedLanguage);
        _messages.Add(message);
        LastMessageAtUtc = DateTimeOffset.UtcNow;

        if (PreferredLanguage == Language.Unknown && detectedLanguage != Language.Unknown)
            PreferredLanguage = detectedLanguage;

        if (Status == ConversationStatus.Closed)
            Status = ConversationStatus.Open;

        return message;
    }

    public Message AddOutboundMessage(string content, MessageOrigin origin)
    {
        var message = new Message(Id, MessageDirection.Outbound, origin, content, detectedLanguage: PreferredLanguage);
        _messages.Add(message);
        LastMessageAtUtc = DateTimeOffset.UtcNow;
        return message;
    }

    /// <summary>Returns the most recent messages, oldest first, to be used as AI conversational context.</summary>
    public IReadOnlyList<Message> GetRecentContext(int maxMessages) =>
        _messages.OrderByDescending(m => m.CreatedAtUtc).Take(maxMessages).Reverse().ToList();

    public void EscalateToHuman() => Status = ConversationStatus.WaitingForHuman;

    /// <summary>
    /// Called when a queued/retried AI reply finally succeeds after an earlier escalation -
    /// the customer got an answer after all, so there's no need to keep a human flagged
    /// unless a new message re-escalates it later. Only reopens from WaitingForHuman;
    /// never touches a conversation a human deliberately Closed.
    /// </summary>
    public void ResolveEscalation()
    {
        if (Status == ConversationStatus.WaitingForHuman)
            Status = ConversationStatus.Open;
    }

    public void Close() => Status = ConversationStatus.Closed;

    /// <summary>Advances (or ends, with ConversationFlowStep.None) the guided menu flow.</summary>
    public void SetFlowStep(ConversationFlowStep step, string? pendingFlowData = null)
    {
        FlowStep = step;
        PendingFlowData = pendingFlowData;
    }

    public void LinkLead(Guid leadId) => LeadId = leadId;

    public void LinkMember(Guid memberId) => MemberId = memberId;
}
