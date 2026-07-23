using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record CreateFlowRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("categories")] List<string> Categories);