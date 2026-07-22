namespace GymChatAI.Application.Abstractions;

/// <summary>Live compliance snapshot for one WhatsApp Business phone number, as reported by Meta.</summary>
public record WhatsAppPhoneNumberHealth(
    string QualityRating,
    string? MessagingLimit,
    string? NameStatus);

/// <summary>
/// Port for reading a phone number's compliance-relevant health data (not sending messages -
/// see IWhatsAppMessageSender for that). Kept separate since it serves a different concern
/// (the Compliance Dashboard) with a different failure-handling story: a health check
/// failing shouldn't ever block message sending, and vice versa.
/// </summary>
public interface IWhatsAppComplianceClient
{
    Task<WhatsAppPhoneNumberHealth> GetPhoneNumberHealthAsync(string phoneNumberId, CancellationToken cancellationToken = default);
}
