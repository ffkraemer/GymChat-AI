using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

// ---- Inbound: button_reply / list_reply (parsed from the webhook payload) ----

public record WhatsAppInteractivePayload(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("button_reply")] WhatsAppInteractiveReply? ButtonReply,
    [property: JsonPropertyName("list_reply")] WhatsAppInteractiveReply? ListReply,
    [property: JsonPropertyName("nfm_reply")] WhatsAppNfmReply? NfmReply);

/// <summary>A completed WhatsApp Flow submission - response_json is itself a JSON string that needs a second parse.</summary>
public record WhatsAppNfmReply(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("response_json")] string? ResponseJson);

public record WhatsAppInteractiveReply(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title);