using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record SendTemplateMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("template")] TemplatePayload Template)
{
    public static SendTemplateMessageRequest Create(string to, string templateName, string templateLanguage, IReadOnlyList<string> parameterValues)
    {
        var components = parameterValues.Count > 0
            ? new List<TemplateComponentPayload> { new("body", parameterValues.Select(v => new TemplateParameterPayload("text", v)).ToList()) }
            : null;

        return new SendTemplateMessageRequest("whatsapp", to, "template", new TemplatePayload(templateName, new TemplateLanguagePayload(templateLanguage), components));
    }
}

internal record TemplatePayload(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("language")] TemplateLanguagePayload Language,
    [property: JsonPropertyName("components")] List<TemplateComponentPayload>? Components);

internal record TemplateLanguagePayload([property: JsonPropertyName("code")] string Code);

internal record TemplateComponentPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("parameters")] List<TemplateParameterPayload> Parameters);

internal record TemplateParameterPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);