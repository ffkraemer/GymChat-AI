using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// One "send me suggestions on this day, in this time window" slot. A member can have
/// several (e.g. Monday mornings AND Wednesday evenings) - see NotificationPreference.Slots.
/// </summary>
public class NotificationTimeSlot : Entity
{
    public Guid NotificationPreferenceId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public NotificationTimeWindow TimeWindow { get; private set; }

    private NotificationTimeSlot() { }

    public NotificationTimeSlot(Guid notificationPreferenceId, DayOfWeek dayOfWeek, NotificationTimeWindow timeWindow)
    {
        NotificationPreferenceId = notificationPreferenceId;
        DayOfWeek = dayOfWeek;
        TimeWindow = timeWindow;
    }
}
