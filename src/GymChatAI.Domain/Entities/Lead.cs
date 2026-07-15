using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents a potential customer captured through a WhatsApp conversation.</summary>
public class Lead : Entity
{
    public Lead(Guid gymId, string phoneNumber, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Lead phone number is required.", nameof(phoneNumber));

        GymId = gymId;
        PhoneNumber = phoneNumber;
        Name = name;
    }

    private Lead()
    { }

    public Guid GymId { get; private set; }

    public string? InterestNotes { get; private set; }

    public string? Name { get; private set; }

    public string PhoneNumber { get; private set; } = default!;

    public LeadStatus Status { get; private set; } = LeadStatus.New;

    public void Convert() => Status = LeadStatus.Converted;

    public void MarkContacted() => Status = LeadStatus.Contacted;

    public void MarkLost() => Status = LeadStatus.Lost;

    public void Qualify(string? notes = null)
    {
        Status = LeadStatus.Qualified;
        if (!string.IsNullOrWhiteSpace(notes)) InterestNotes = notes;
    }

    public void UpdateName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
    }
}