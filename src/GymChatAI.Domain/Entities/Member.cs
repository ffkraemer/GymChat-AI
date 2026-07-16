using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>Represents a gym member (converted lead / active customer).</summary>
public class Member : Entity
{
    public Member(Guid gymId, string fullName, string phoneNumber, DateOnly? birthDate = null, Guid? planId = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Member name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Member phone number is required.", nameof(phoneNumber));

        GymId = gymId;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        BirthDate = birthDate;
        PlanId = planId;
    }

    private Member()
    { }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>First token of the full name, used to personalize campaign messages (e.g. "{FirstName}").</summary>
    public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;

    public string FullName { get; private set; } = default!;

    public Guid GymId { get; private set; }

    public DateTimeOffset? LastCheckInAtUtc { get; private set; }

    public string PhoneNumber { get; private set; } = default!;

    public Guid? PlanId { get; private set; }

    public MemberStatus Status { get; private set; } = MemberStatus.Active;

    public bool IsInactiveFor(TimeSpan threshold) =>
        LastCheckInAtUtc is null || DateTimeOffset.UtcNow - LastCheckInAtUtc.Value > threshold;

    public void MarkInactive() => Status = MemberStatus.Inactive;

    public void Reactivate() => Status = MemberStatus.Active;

    public void RegisterCheckIn() => LastCheckInAtUtc = DateTimeOffset.UtcNow;
}