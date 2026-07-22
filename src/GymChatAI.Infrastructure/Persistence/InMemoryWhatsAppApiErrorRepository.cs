using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence;

public class InMemoryWhatsAppApiErrorStore
{
    public ConcurrentDictionary<Guid, WhatsAppApiError> Items { get; } = new();
}

public class InMemoryWhatsAppApiErrorRepository : IWhatsAppApiErrorRepository
{
    private readonly InMemoryWhatsAppApiErrorStore _store;

    public InMemoryWhatsAppApiErrorRepository(InMemoryWhatsAppApiErrorStore store) => _store = store;

    public Task<IReadOnlyList<WhatsAppApiError>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WhatsAppApiError> result = _store.Items.Values
            .Where(e => e.GymId == gymId && e.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(WhatsAppApiError error, CancellationToken cancellationToken = default)
    {
        _store.Items[error.Id] = error;
        return Task.CompletedTask;
    }
}
