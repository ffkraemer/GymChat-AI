using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record CreateTemplateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("components")] List<TemplateComponent> Components);

internal record TemplateComponent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("example")] TemplateComponentExample? Example);

internal record TemplateComponentExample(
    [property: JsonPropertyName("body_text")] List<List<string>> BodyText);

internal record CreateTemplateResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status);

internal record ListTemplatesResponse(
    [property: JsonPropertyName("data")] List<TemplateListItem>? Data);

internal record TemplateListItem(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("rejected_reason")] string? RejectedReason);
