using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;



public record WhatsAppChange(
    [property: JsonPropertyName("value")] WhatsAppChangeValue? Value,
    [property: JsonPropertyName("field")] string? Field);

public record WhatsAppChangeValue(
    [property: JsonPropertyName("messaging_product")] string? MessagingProduct,
    [property: JsonPropertyName("metadata")] WhatsAppMetadata? Metadata,
    [property: JsonPropertyName("contacts")] List<WhatsAppContact>? Contacts,
    [property: JsonPropertyName("messages")] List<WhatsAppInboundMessage>? Messages,
    [property: JsonPropertyName("statuses")] List<WhatsAppStatus>? Statuses);

public record WhatsAppContact(
    [property: JsonPropertyName("profile")] WhatsAppProfile? Profile,
    [property: JsonPropertyName("wa_id")] string? WaId);

public record WhatsAppProfile([property: JsonPropertyName("name")] string? Name);

public record WhatsAppInboundMessage(
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WhatsAppTextBody? Text);

public record WhatsAppTextBody([property: JsonPropertyName("body")] string? Body);

public record WhatsAppStatus(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("recipient_id")] string? RecipientId);
