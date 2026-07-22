namespace GymChatAI.Application.Abstractions;

/// <summary>
/// Thrown by IWhatsAppMessageSender implementations when an identical message was already
/// sent to the same recipient too recently - a guard against accidentally spamming one
/// person, which risks the WhatsApp Business number's quality rating. Callers that already
/// wrap sends in a try/catch (see ProcessIncomingMessageHandler, LoyaltyEngineHandler) treat
/// this the same as any other send failure; they don't need special handling unless they want to.
/// </summary>
public class DuplicateMessageException : Exception
{
    public DuplicateMessageException(string recipientPhoneNumber)
        : base($"An identical message was sent to {recipientPhoneNumber} too recently - skipped to protect the number's WhatsApp quality rating.")
    {
    }
}
