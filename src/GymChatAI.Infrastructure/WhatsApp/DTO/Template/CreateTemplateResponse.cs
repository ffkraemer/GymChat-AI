using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

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