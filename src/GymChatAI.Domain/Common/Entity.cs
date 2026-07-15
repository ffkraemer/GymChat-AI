namespace GymChatAI.Domain.Common;

/// <summary>
/// Base class for all domain entities. Provides identity-based equality,
/// which is the cornerstone of entity semantics in DDD (as opposed to value objects).
/// </summary>
public abstract class Entity
{
    public DateTimeOffset CreatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    protected void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}