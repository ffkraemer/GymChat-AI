using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// One contact's (lead or member) notification preferences, captured through the guided
/// WhatsApp menu (see OnboardingFlowHandler in Application). Keyed by GymId + phone number
/// rather than MemberId, because onboarding happens on first contact - before the platform
/// necessarily knows whether this person will become a paying Member.
/// </summary>
public class NotificationPreference : Entity
{
    private readonly List<NotificationTimeSlot> _slots = new();

    public Guid GymId { get; private set; }

    public string ContactPhoneNumber { get; private set; } = default!;

    /// <summary>Whether the person has been through the onboarding menu at least once (regardless of what they chose).</summary>
    public bool IsOnboarded { get; private set; }

    public bool OptedIntoNotifications { get; private set; }

    /// <summary>
    /// Plain settable list (rather than a private-field + AsReadOnly wrapper like Slots)
    /// so EF Core's built-in primitive-collection support can map it directly to a JSON
    /// column with zero custom configuration - simpler and safer than hand-writing a
    /// value converter for this.
    /// </summary>
    public List<Guid> SelectedClassTypeIds { get; private set; } = new();

    public IReadOnlyCollection<NotificationTimeSlot> Slots => _slots.AsReadOnly();

    private NotificationPreference() { }

    public NotificationPreference(Guid gymId, string contactPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(contactPhoneNumber))
            throw new ArgumentException("Contact phone number is required.", nameof(contactPhoneNumber));

        GymId = gymId;
        ContactPhoneNumber = contactPhoneNumber;
    }

    public void CompleteOnboarding(bool optedIn)
    {
        IsOnboarded = true;
        OptedIntoNotifications = optedIn;
    }

    public void SelectClassType(Guid classTypeId)
    {
        if (!SelectedClassTypeIds.Contains(classTypeId))
            SelectedClassTypeIds.Add(classTypeId);
    }

    public void ClearClassTypeSelections() => SelectedClassTypeIds.Clear();

    public void AddTimeSlot(DayOfWeek dayOfWeek, NotificationTimeWindow timeWindow)
    {
        var alreadyExists = _slots.Any(s => s.DayOfWeek == dayOfWeek && s.TimeWindow == timeWindow);
        if (alreadyExists) return;

        _slots.Add(new NotificationTimeSlot(Id, dayOfWeek, timeWindow));
    }

    /// <summary>Lets the member start over (e.g. re-running the menu from scratch).</summary>
    public void ResetSelections()
    {
        SelectedClassTypeIds.Clear();
        _slots.Clear();
    }
}
