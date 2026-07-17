using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Represents a customer message that still needs an AI-generated reply, because the AI
/// provider was unavailable (rate-limited, transient outage, etc.) when we first tried.
/// A background process (see PendingAIReplyBackgroundService in Infrastructure) retries
/// these periodically, so a customer's question is never silently left unanswered just
/// because the AI happened to hiccup at that exact moment.
/// </summary>
public class PendingAIReply : Entity
{
    private const int DefaultMaxAttempts = 5;

    public Guid ConversationId { get; private set; }

    public Guid GymId { get; private set; }

    public string UserMessage { get; private set; } = default!;

    public int Attempts { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public PendingAIReplyStatus Status { get; private set; } = PendingAIReplyStatus.Pending;

    private PendingAIReply() { }

    public PendingAIReply(Guid conversationId, Guid gymId, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message is required.", nameof(userMessage));

        ConversationId = conversationId;
        GymId = gymId;
        UserMessage = userMessage;
    }

    public void MarkResolved() => Status = PendingAIReplyStatus.Resolved;

    /// <summary>Records a failed retry attempt; gives up (Abandoned) after <paramref name="maxAttempts"/>.</summary>
    public void RecordFailedAttempt(string error, int maxAttempts = DefaultMaxAttempts)
    {
        Attempts++;
        LastAttemptAtUtc = DateTimeOffset.UtcNow;
        LastError = error;

        if (Attempts >= maxAttempts)
            Status = PendingAIReplyStatus.Abandoned;
    }
}
