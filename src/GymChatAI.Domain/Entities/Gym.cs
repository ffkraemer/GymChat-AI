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
    public string Name { get; private set; } = default!;

    public string WhatsAppPhoneNumberId { get; private set; } = default!;

    public string WhatsAppDisplayPhoneNumber { get; private set; } = default!;

    /// <summary>
    /// The WhatsApp Business Account (WABA) id that owns this gym's phone number. Optional
    /// because it's only needed for template management (which operates at the WABA level,
    /// not the phone number level) - existing gyms set up before this feature won't have it
    /// until an admin fills it in.
    /// </summary>
    public string? WhatsAppBusinessAccountId { get; private set; }

    public Language DefaultLanguage { get; private set; } = Language.Portuguese;

    public bool IsActive { get; private set; } = true;

    private Gym() { }

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

    public void Deactivate() => IsActive = false;

    public void SetWhatsAppBusinessAccountId(string whatsAppBusinessAccountId)
    {
        if (string.IsNullOrWhiteSpace(whatsAppBusinessAccountId))
            throw new ArgumentException("WhatsApp Business Account id is required.", nameof(whatsAppBusinessAccountId));

        WhatsAppBusinessAccountId = whatsAppBusinessAccountId;
    }
}
