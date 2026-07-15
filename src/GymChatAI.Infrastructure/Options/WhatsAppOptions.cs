namespace GymChatAI.Infrastructure.Options;

/// <summary>Configuration bound from appsettings/environment for WhatsApp Business Cloud API.</summary>
public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Permanent or system-user access token for the WhatsApp Business Cloud API.</summary>
    public string AccessToken { get; set; } = default!;

    /// <summary>Graph API base url, e.g. https://graph.facebook.com/v21.0</summary>
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com/v21.0";

    /// <summary>Shared secret used to verify the webhook subscription (hub.verify_token).</summary>
    public string WebhookVerifyToken { get; set; } = default!;

    /// <summary>Optional app secret used to validate X-Hub-Signature-256 on incoming webhooks.</summary>
    public string? AppSecret { get; set; }

    /// <summary>
    /// Phone number id used to seed the demo gym for local/POC testing.
    /// Set this to match the WhatsApp test number configured in Meta's App Dashboard.
    /// </summary>
    public string DemoPhoneNumberId { get; set; } = "DEMO_PHONE_NUMBER_ID";
}
