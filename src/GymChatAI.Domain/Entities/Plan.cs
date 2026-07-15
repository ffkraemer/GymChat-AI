using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents a membership plan offered by a gym.</summary>
public class Plan : Entity
{
    public Plan(Guid gymId, string name, string description, decimal monthlyPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan name is required.", nameof(name));
        if (monthlyPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyPrice));

        GymId = gymId;
        Name = name;
        Description = description;
        MonthlyPrice = monthlyPrice;
    }

    private Plan()
    { }

    public string Description { get; private set; } = default!;

    public Guid GymId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public decimal MonthlyPrice { get; private set; }

    public string Name { get; private set; } = default!;

    public void Deactivate() => IsActive = false;
}