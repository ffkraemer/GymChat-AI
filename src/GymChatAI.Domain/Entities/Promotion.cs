using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents an active promotion that the AI assistant can mention to leads/members.</summary>
public class Promotion : Entity
{
    public Guid GymId { get; private set; }

    public string Title { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>
    /// Soft-delete flag, consistent with Gym/Faq/Plan: promotions are never hard-deleted,
    /// only deactivated. This keeps historical/audit data intact and avoids ever needing
    /// cascade deletes at the database level.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    private Promotion() { }

    public Promotion(Guid gymId, string title, string description, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Promotion title is required.", nameof(title));
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        GymId = gymId;
        Title = title;
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
    }

    public bool IsActiveOn(DateOnly date) => IsActive && date >= StartDate && date <= EndDate;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
