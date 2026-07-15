namespace GymChatAI.Application.Messaging;

/// <summary>
/// Transport-agnostic representation of an inbound WhatsApp message, already
/// parsed out of the raw webhook payload by the Infrastructure layer.
/// </summary>
public record IncomingWhatsAppMessage(
    string WhatsAppPhoneNumberId,
    string FromPhoneNumber,
    string? ContactName,
    string Text,
    string WhatsAppMessageId,
    DateTimeOffset TimestampUtc);
