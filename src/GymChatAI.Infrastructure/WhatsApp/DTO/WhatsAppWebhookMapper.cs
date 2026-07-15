using System.Globalization;
using GymChatAI.Application.Messaging;

namespace GymChatAI.Infrastructure.WhatsApp;

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
                    // POC scope: only free-text messages. Media/interactive types are ignored for now.
                    if (message.Type != "text" || message.Text?.Body is null || message.From is null || message.Id is null)
                        continue;

                    var contactName = value.Contacts?
                        .FirstOrDefault(c => c.WaId == message.From)?
                        .Profile?.Name;

                    var timestamp = ParseTimestamp(message.Timestamp);

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
