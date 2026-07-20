using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

// ---- Outbound: interactive list message (up to 10 rows total) ----

internal record SendListMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("interactive")] ListInteractive Interactive)
{
    public static SendListMessageRequest Create(string to, string bodyText, string buttonText, List<ListSectionPayload> sections) =>
        new("whatsapp", to, "interactive", new ListInteractive("list", new InteractiveBody(bodyText), new ListAction(buttonText, sections)));
}

internal record ListInteractive(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("body")] InteractiveBody Body,
    [property: JsonPropertyName("action")] ListAction Action);

internal record ListAction(
    [property: JsonPropertyName("button")] string Button,
    [property: JsonPropertyName("sections")] List<ListSectionPayload> Sections);

internal record ListSectionPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("rows")] List<ListRowPayload> Rows);

internal record ListRowPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description);

internal record InteractiveBody([property: JsonPropertyName("text")] string Text);