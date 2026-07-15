using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents an active promotion that the AI assistant can mention to leads/members.</summary>
public class Promotion : Entity
{
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

    private Promotion()
    { }

    public string Description { get; private set; } = default!;

    public DateOnly EndDate { get; private set; }

    public Guid GymId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public string Title { get; private set; } = default!;

    public bool IsActiveOn(DateOnly date) => date >= StartDate && date <= EndDate;
}