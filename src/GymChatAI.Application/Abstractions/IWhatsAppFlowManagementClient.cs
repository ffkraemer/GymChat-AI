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

    Task<bool> PublishFlowAsync(string metaFlowId, CancellationToken cancellationToken = default);

    /// <summary>Returns Meta's current status string (e.g. "DRAFT", "PUBLISHED") for this flow.</summary>
    Task<string?> GetFlowStatusAsync(string metaFlowId, CancellationToken cancellationToken = default);
}
