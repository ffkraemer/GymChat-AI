using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IPendingAIReplyRepository
{
    /// <summary>All replies still awaiting a retry, across every gym - the background service iterates this directly.</summary>
    Task<IReadOnlyList<PendingAIReply>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default);

    Task UpdateAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default);
}
