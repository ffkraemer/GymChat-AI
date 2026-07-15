namespace GymChatAI.Application.Abstractions;

/// <summary>
/// Port for sending outbound messages through WhatsApp Business Cloud API.
/// Implemented in Infrastructure so Application stays framework/HTTP agnostic.
/// </summary>
public interface IWhatsAppMessageSender
{
    /// <param name="fromPhoneNumberId">The gym's WhatsApp Business phone number id (sender).</param>
    /// <param name="toPhoneNumber">The recipient's phone number (E.164 format, no '+').</param>
    /// <param name="text">The message body.</param>
    /// <returns>WhatsApp's message id (wamid) for the sent message.</returns>
    Task<string> SendTextMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string text,
        CancellationToken cancellationToken = default);
}
