using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

// ---- Outbound: interactive button message (up to 3 buttons) ----

internal record SendButtonMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("interactive")] ButtonInteractive Interactive)
{
    public static SendButtonMessageRequest Create(string to, string bodyText, List<ButtonPayload> buttons) =>
        new("whatsapp", to, "interactive", new ButtonInteractive("button", new InteractiveBody(bodyText), new ButtonAction(buttons)));
}

internal record ButtonInteractive(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("body")] InteractiveBody Body,
    [property: JsonPropertyName("action")] ButtonAction Action);

internal record ButtonAction([property: JsonPropertyName("buttons")] List<ButtonPayload> Buttons);

internal record ButtonPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("reply")] ButtonReplyPayload Reply)
{
    public static ButtonPayload Create(string id, string title) => new("reply", new ButtonReplyPayload(id, title));
}

internal record ButtonReplyPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);