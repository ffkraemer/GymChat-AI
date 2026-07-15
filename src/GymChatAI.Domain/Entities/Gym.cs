using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Represents a gym using the platform. Acts as the tenant boundary,
/// even though the POC runs as a single tenant (future SaaS phase will
/// use GymId as the partition/isolation key).
/// </summary>
public class Gym : Entity
{
    public Gym(string name, string whatsAppPhoneNumberId, string whatsAppDisplayPhoneNumber, Language defaultLanguage = Language.Portuguese)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Gym name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(whatsAppPhoneNumberId))
            throw new ArgumentException("WhatsApp phone number id is required.", nameof(whatsAppPhoneNumberId));

        Name = name;
        WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
        WhatsAppDisplayPhoneNumber = whatsAppDisplayPhoneNumber;
        DefaultLanguage = defaultLanguage;
    }

    private Gym()
    { }

    public Language DefaultLanguage { get; private set; } = Language.Portuguese;

    public bool IsActive { get; private set; } = true;

    public string Name { get; private set; } = default!;

    public string WhatsAppDisplayPhoneNumber { get; private set; } = default!;

    public string WhatsAppPhoneNumberId { get; private set; } = default!;

    public void Deactivate() => IsActive = false;
}