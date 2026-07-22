using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

/// <summary>Shape of GET /{phone-number-id}?fields=quality_rating,whatsapp_business_manager_messaging_limit,name_status.</summary>
internal record WhatsAppPhoneNumberHealthResponse(
    [property: JsonPropertyName("quality_rating")] string? QualityRating,
    [property: JsonPropertyName("whatsapp_business_manager_messaging_limit")] string? MessagingLimit,
    [property: JsonPropertyName("name_status")] string? NameStatus);
