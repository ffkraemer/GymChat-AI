namespace GymChatAI.Application.Abstractions;

/// <summary>A single tappable button (WhatsApp allows at most 3 per message).</summary>
public record WhatsAppButtonOption(string Id, string Title);

/// <summary>A single row within a list message section (WhatsApp allows at most 10 rows total).</summary>
public record WhatsAppListRow(string Id, string Title, string? Description = null);

/// <summary>A named group of rows within a list message.</summary>
public record WhatsAppListSection(string Title, IReadOnlyList<WhatsAppListRow> Rows);

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

    /// <summary>Sends up to 3 tappable reply buttons - for short yes/no-style menu steps.</summary>
    Task<string> SendButtonMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        IReadOnlyList<WhatsAppButtonOption> buttons,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a scrollable list menu (up to 10 rows total) - for menus with more than 3 options.</summary>
    Task<string> SendListMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        string buttonText,
        IReadOnlyList<WhatsAppListSection> sections,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a message that opens a native WhatsApp Flow (multi-field form).</summary>
    /// <param name="metaFlowId">Meta's own id for the published Flow.</param>
    /// <param name="flowToken">A token we generate to correlate this session with the Data Exchange endpoint later.</param>
    /// <param name="screenId">The initial screen to open.</param>
    Task<string> SendFlowMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        string flowCtaButtonText,
        string metaFlowId,
        string flowToken,
        string screenId,
        CancellationToken cancellationToken = default);
}
