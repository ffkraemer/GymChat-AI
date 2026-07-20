using GymChatAI.Application.Messaging;
using System.Globalization;

namespace GymChatAI.Infrastructure.WhatsApp.Mapper;

/// <summary>Translates WhatsApp Cloud API webhook payloads into Application-layer messages.</summary>
public static class WhatsAppWebhookMapper
{
    public static IReadOnlyList<IncomingWhatsAppMessage> ExtractIncomingMessages(WhatsAppWebhookPayload payload)
    {
        var results = new List<IncomingWhatsAppMessage>();

        foreach (var entry in payload.Entry ?? Enumerable.Empty<WhatsAppEntry>())
        {
            foreach (var change in entry.Changes ?? Enumerable.Empty<WhatsAppChange>())
            {
                var value = change.Value;
                if (value?.Messages is null || value.Metadata?.PhoneNumberId is null)
                    continue;

                foreach (var message in value.Messages)
                {
                    if (message.From is null || message.Id is null) continue;

                    var contactName = value.Contacts?
                        .FirstOrDefault(c => c.WaId == message.From)?
                        .Profile?.Name;

                    var timestamp = ParseTimestamp(message.Timestamp);

                    // A tapped button/list row: no free text, just the id of whichever
                    // WhatsAppButtonOption/WhatsAppListRow was chosen.
                    if (message.Type == "interactive" && message.Interactive is not null)
                    {
                        var replyId = message.Interactive.ButtonReply?.Id ?? message.Interactive.ListReply?.Id;
                        if (replyId is null) continue;

                        results.Add(new IncomingWhatsAppMessage(
                            WhatsAppPhoneNumberId: value.Metadata.PhoneNumberId,
                            FromPhoneNumber: message.From,
                            ContactName: contactName,
                            Text: string.Empty,
                            WhatsAppMessageId: message.Id,
                            TimestampUtc: timestamp,
                            InteractiveReplyId: replyId));
                        continue;
                    }

                    // Free-text messages. Other types (media, location, etc.) are ignored for now.
                    if (message.Type != "text" || message.Text?.Body is null) continue;

                    results.Add(new IncomingWhatsAppMessage(
                        WhatsAppPhoneNumberId: value.Metadata.PhoneNumberId,
                        FromPhoneNumber: message.From,
                        ContactName: contactName,
                        Text: message.Text.Body,
                        WhatsAppMessageId: message.Id,
                        TimestampUtc: timestamp));
                }
            }
        }

        return results;
    }

    private static DateTimeOffset ParseTimestamp(string? unixSeconds)
    {
        if (long.TryParse(unixSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return DateTimeOffset.FromUnixTimeSeconds(seconds);

        return DateTimeOffset.UtcNow;
    }
}