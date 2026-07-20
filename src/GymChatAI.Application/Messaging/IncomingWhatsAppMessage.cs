namespace GymChatAI.Application.Messaging;

/// <summary>
/// Transport-agnostic representation of an inbound WhatsApp message, already
/// parsed out of the raw webhook payload by the Infrastructure layer.
/// Text and InteractiveReplyId are mutually exclusive in practice: a plain text message
/// has Text set and InteractiveReplyId null; a button/list tap has InteractiveReplyId set
/// (the id of whichever WhatsAppButtonOption/WhatsAppListRow was tapped) and Text empty.
/// </summary>
public record IncomingWhatsAppMessage(
    string WhatsAppPhoneNumberId,
    string FromPhoneNumber,
    string? ContactName,
    string Text,
    string WhatsAppMessageId,
    DateTimeOffset TimestampUtc,
    string? InteractiveReplyId = null);
