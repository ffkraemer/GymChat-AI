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