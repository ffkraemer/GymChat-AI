using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents a single WhatsApp message exchanged within a conversation.</summary>
public class Message : Entity
{
    public Message(
        Guid conversationId,
        MessageDirection direction,
        MessageOrigin origin,
        string content,
        string? whatsAppMessageId = null,
        Language detectedLanguage = Language.Unknown)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty.", nameof(content));

        ConversationId = conversationId;
        Direction = direction;
        Origin = origin;
        Content = content;
        WhatsAppMessageId = whatsAppMessageId;
        DetectedLanguage = detectedLanguage;
        Status = direction == MessageDirection.Inbound ? MessageStatus.Received : MessageStatus.Processing;
    }

    private Message()
    { }

    public string Content { get; private set; } = default!;

    public Guid ConversationId { get; private set; }

    public Language DetectedLanguage { get; private set; } = Language.Unknown;

    public MessageDirection Direction { get; private set; }

    public MessageOrigin Origin { get; private set; }

    public MessageStatus Status { get; private set; } = MessageStatus.Received;

    /// <summary>WhatsApp's own message id (wamid), used for idempotency and status callbacks.</summary>
    public string? WhatsAppMessageId { get; private set; }

    public void MarkDelivered() => Status = MessageStatus.Delivered;

    public void MarkFailed() => Status = MessageStatus.Failed;

    public void MarkRead() => Status = MessageStatus.Read;

    public void MarkSent(string whatsAppMessageId)
    {
        WhatsAppMessageId = whatsAppMessageId;
        Status = MessageStatus.Sent;
    }

    public void SetDetectedLanguage(Language language) => DetectedLanguage = language;
}