namespace GymChatAI.Application.Abstractions;

public record WhatsAppFlowValidationError(string? Error, string? Message);

public record CreateFlowResult(string MetaFlowId, IReadOnlyList<WhatsAppFlowValidationError> ValidationErrors);

public record UpdateFlowJsonResult(bool Success, IReadOnlyList<WhatsAppFlowValidationError> ValidationErrors);

/// <summary>
/// Port for managing a WhatsApp Flow's lifecycle via the Graph API: create, upload/replace
/// its JSON definition, publish, and check status - so this can all happen from our
/// Administration Portal instead of Meta's WhatsApp Manager UI.
/// </summary>
public interface IWhatsAppFlowManagementClient
{
    /// <summary>
    /// One-time setup per phone number: registers our RSA public key so Meta can encrypt
    /// Data Exchange requests to us. Unlike the other methods here, this one is scoped to
    /// the phone number id, not the WABA id - Meta's endpoint is
    /// /{phone-number-id}/whatsapp_business_encryption.
    /// </summary>
    Task<bool> RegisterEncryptionKeyAsync(string phoneNumberId, string publicKeyPem, CancellationToken cancellationToken = default);

    Task<CreateFlowResult> CreateFlowAsync(string whatsAppBusinessAccountId, string name, IReadOnlyList<string> categories, CancellationToken cancellationToken = default);

    Task<UpdateFlowJsonResult> UpdateFlowJsonAsync(string metaFlowId, string flowJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells Meta where to send Data Exchange requests for this Flow - required for any
    /// Flow whose JSON declares dynamic data (our preferences form does, since the class
    /// type list comes from the gym's own data). Without this, Meta rejects publishing with
    /// a validation error about the missing/unreachable endpoint.
    /// </summary>
    Task<bool> SetFlowEndpointAsync(string metaFlowId, string endpointUri, CancellationToken cancellationToken = default);

    /// <summary>Returns the error body from Meta when publishing fails, so the admin sees the real reason instead of a generic message.</summary>
    Task<(bool Success, string? ErrorMessage)> PublishFlowAsync(string metaFlowId, CancellationToken cancellationToken = default);

    /// <summary>Returns Meta's current status string (e.g. "DRAFT", "PUBLISHED") for this flow.</summary>
    Task<string?> GetFlowStatusAsync(string metaFlowId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Flow on Meta's side - only DRAFT flows can be deleted; a published one has to be deprecated instead.</summary>
    Task<bool> DeleteFlowAsync(string metaFlowId, CancellationToken cancellationToken = default);
}
