namespace GymChatAI.Infrastructure.Options;

/// <summary>Configuration for the WhatsApp Flows Data Exchange endpoint (encryption keys).</summary>
public class WhatsAppFlowOptions
{
    public const string SectionName = "WhatsAppFlow";

    /// <summary>PEM-encoded RSA private key, used to decrypt Data Exchange requests from Meta. Keep this in user-secrets/a real secret store - never commit it.</summary>
    public string PrivateKeyPem { get; set; } = default!;

    /// <summary>Password protecting the private key, if it was generated with one (recommended).</summary>
    public string? PrivateKeyPassword { get; set; }

    /// <summary>PEM-encoded RSA public key - registered with Meta via IWhatsAppFlowManagementClient.RegisterEncryptionKeyAsync.</summary>
    public string PublicKeyPem { get; set; } = default!;
}
