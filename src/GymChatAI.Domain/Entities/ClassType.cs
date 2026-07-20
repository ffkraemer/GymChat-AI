using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// A type of class a gym offers (e.g. "Yoga", "Spinning"), configured by the gym's admin
/// in the Administration Portal. Used to build the WhatsApp list menu members choose from
/// when setting their notification preferences - never hard-coded, since every gym runs
/// different classes.
/// </summary>
public class ClassType : Entity
{
    public Guid GymId { get; private set; }

    public string Name { get; private set; } = default!;

    public bool IsActive { get; private set; } = true;

    private ClassType() { }

    public ClassType(Guid gymId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Class type name is required.", nameof(name));

        GymId = gymId;
        Name = name;
    }

    public void Rename(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;
        Touch();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
