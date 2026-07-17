using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Infrastructure.Persistence;

/// <summary>
/// Standalone process-memory store for pending AI replies. Kept in its own tiny class
/// (rather than folded into InMemoryDataStore) so it can be added without needing to
/// touch/guess the exact current shape of that file.
/// </summary>
public class InMemoryPendingAIReplyStore
{
    public ConcurrentDictionary<Guid, PendingAIReply> Items { get; } = new();
}

public class InMemoryPendingAIReplyRepository : IPendingAIReplyRepository
{
    private readonly InMemoryPendingAIReplyStore _store;

    public InMemoryPendingAIReplyRepository(InMemoryPendingAIReplyStore store) => _store = store;

    public Task<IReadOnlyList<PendingAIReply>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingAIReply> result = _store.Items.Values
            .Where(p => p.Status == PendingAIReplyStatus.Pending)
            .OrderBy(p => p.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default)
    {
        _store.Items[pendingReply.Id] = pendingReply;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default)
    {
        _store.Items[pendingReply.Id] = pendingReply;
        return Task.CompletedTask;
    }
}
