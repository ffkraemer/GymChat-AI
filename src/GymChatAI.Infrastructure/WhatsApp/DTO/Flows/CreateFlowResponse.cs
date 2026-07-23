using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record CreateFlowResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("validation_errors")] List<FlowValidationErrorDto>? ValidationErrors);

internal record FlowValidationErrorDto(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message);

internal record UpdateFlowJsonResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("validation_errors")] List<FlowValidationErrorDto>? ValidationErrors);

internal record PublishFlowResponse([property: JsonPropertyName("success")] bool Success);

internal record FlowStatusResponse([property: JsonPropertyName("status")] string? Status);

internal record RegisterEncryptionKeyResponse([property: JsonPropertyName("success")] bool Success);