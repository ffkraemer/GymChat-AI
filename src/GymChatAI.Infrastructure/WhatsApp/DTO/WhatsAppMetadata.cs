using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

public record WhatsAppMetadata(
    [property: JsonPropertyName("display_phone_number")] string? DisplayPhoneNumber,
    [property: JsonPropertyName("phone_number_id")] string? PhoneNumberId);
