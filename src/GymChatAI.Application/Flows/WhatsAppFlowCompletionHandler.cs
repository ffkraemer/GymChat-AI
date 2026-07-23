using System.Text.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Processes a completed WhatsApp Flow submission (the "PREFERENCES" form built by
/// PreferencesFlowJsonBuilder) into a NotificationPreference - the Flow-based equivalent of
/// what OnboardingFlowHandler does step by step with buttons/lists, but here it all arrives
/// as one payload since the whole form was filled out natively in a single screen.
/// </summary>
public class WhatsAppFlowCompletionHandler
{
    private readonly INotificationPreferenceRepository _preferenceRepository;

    public WhatsAppFlowCompletionHandler(INotificationPreferenceRepository preferenceRepository)
    {
        _preferenceRepository = preferenceRepository;
    }

    public async Task HandleAsync(Guid gymId, string contactPhoneNumber, string responseJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var selectedClassIds = new List<Guid>();
        if (root.TryGetProperty("selected_classes", out var classesEl) && classesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in classesEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var classId))
                    selectedClassIds.Add(classId);
            }
        }

        DayOfWeek? preferredDay = null;
        if (root.TryGetProperty("preferred_day", out var dayEl) && dayEl.ValueKind == JsonValueKind.String
            && int.TryParse(dayEl.GetString(), out var dayRaw) && dayRaw is >= 0 and <= 6)
        {
            preferredDay = (DayOfWeek)dayRaw;
        }

        NotificationTimeWindow? preferredWindow = root.TryGetProperty("preferred_window", out var windowEl) && windowEl.ValueKind == JsonValueKind.String
            ? windowEl.GetString() switch
            {
                "morning" => NotificationTimeWindow.Morning,
                "afternoon" => NotificationTimeWindow.Afternoon,
                "evening" => NotificationTimeWindow.Evening,
                _ => (NotificationTimeWindow?)null
            }
            : null;

        var existing = await _preferenceRepository.GetByContactAsync(gymId, contactPhoneNumber, cancellationToken);
        var isNew = existing is null;
        var preference = existing ?? new NotificationPreference(gymId, contactPhoneNumber);

        // Leaving everything unmarked in the form (no classes, no day/window) is how the
        // Flow's UI expresses "I don't want notifications" - see the screen's helper text.
        var optedIn = selectedClassIds.Count > 0 || (preferredDay is not null && preferredWindow is not null);
        preference.CompleteOnboarding(optedIn);

        preference.ClearClassTypeSelections();
        foreach (var classId in selectedClassIds)
            preference.SelectClassType(classId);

        preference.ClearTimeSlots();
        if (preferredDay is not null && preferredWindow is not null)
            preference.AddTimeSlot(preferredDay.Value, preferredWindow.Value);

        if (isNew)
            await _preferenceRepository.AddAsync(preference, cancellationToken);
        else
            await _preferenceRepository.UpdateAsync(preference, cancellationToken);
    }
}
