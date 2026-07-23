namespace GymChatAI.Application.Messaging;

/// <summary>
/// Transport-agnostic representation of an inbound WhatsApp message, already
/// parsed out of the raw webhook payload by the Infrastructure layer.
/// Text, InteractiveReplyId, and FlowResponseJson are mutually exclusive in practice:
/// a plain text message has Text set; a button/list tap has InteractiveReplyId set (the id
/// of whichever WhatsAppButtonOption/WhatsAppListRow was tapped); a completed WhatsApp Flow
/// submission has FlowResponseJson set (the flow's raw form data, as JSON).
/// </summary>
public record IncomingWhatsAppMessage(
    string WhatsAppPhoneNumberId,
    string FromPhoneNumber,
    string? ContactName,
    string Text,
    string WhatsAppMessageId,
    DateTimeOffset TimestampUtc,
    string? InteractiveReplyId = null,
    string? FlowResponseJson = null);
