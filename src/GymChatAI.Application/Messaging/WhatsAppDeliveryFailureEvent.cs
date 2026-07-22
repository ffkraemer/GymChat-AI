namespace GymChatAI.Application.Messaging;

/// <summary>
/// A "status": "failed" entry from the WhatsApp status webhook - Meta confirming, after
/// the fact, that a message it had accepted could not actually be delivered.
/// </summary>
public record WhatsAppDeliveryFailureEvent(
    string WhatsAppPhoneNumberId,
    string WhatsAppMessageId,
    string RecipientPhoneNumber,
    string? ErrorCode,
    string ErrorMessage);
