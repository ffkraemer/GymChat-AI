using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IPendingAIReplyRepository
{
    /// <summary>All replies still awaiting a retry, across every gym - the background service iterates this directly.</summary>
    Task<IReadOnlyList<PendingAIReply>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>All AI failures for one gym since a given time (any status) - used by the Compliance Dashboard.</summary>
    Task<IReadOnlyList<PendingAIReply>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    Task AddAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default);

    Task UpdateAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default);
}
