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

    public Conversation(Guid gymId, string contactPhoneNumber, Guid? leadId = null, Guid? memberId = null)
    {
        if (string.IsNullOrWhiteSpace(contactPhoneNumber))
            throw new ArgumentException("Contact phone number is required.", nameof(contactPhoneNumber));

        GymId = gymId;
        ContactPhoneNumber = contactPhoneNumber;
        LeadId = leadId;
        MemberId = memberId;
    }

    private Conversation()
    { }

    public string ContactPhoneNumber { get; private set; } = default!;

    public Guid GymId { get; private set; }

    public DateTimeOffset LastMessageAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public Guid? LeadId { get; private set; }

    public Guid? MemberId { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    public Language PreferredLanguage { get; private set; } = Language.Unknown;

    public ConversationStatus Status { get; private set; } = ConversationStatus.Open;

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

    public void Close() => Status = ConversationStatus.Closed;

    public void EscalateToHuman() => Status = ConversationStatus.WaitingForHuman;

    /// <summary>Returns the most recent messages, oldest first, to be used as AI conversational context.</summary>
    public IReadOnlyList<Message> GetRecentContext(int maxMessages) =>
        _messages.OrderByDescending(m => m.CreatedAtUtc).Take(maxMessages).Reverse().ToList();

    public void LinkLead(Guid leadId) => LeadId = leadId;

    public void LinkMember(Guid memberId) => MemberId = memberId;
}