using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence;

public class InMemoryWhatsAppDeliveryFailureStore
{
    public ConcurrentDictionary<Guid, WhatsAppDeliveryFailure> Items { get; } = new();
}

public class InMemoryWhatsAppDeliveryFailureRepository : IWhatsAppDeliveryFailureRepository
{
    private readonly InMemoryWhatsAppDeliveryFailureStore _store;

    public InMemoryWhatsAppDeliveryFailureRepository(InMemoryWhatsAppDeliveryFailureStore store) => _store = store;

    public Task<IReadOnlyList<WhatsAppDeliveryFailure>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WhatsAppDeliveryFailure> result = _store.Items.Values
            .Where(f => f.GymId == gymId && f.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(WhatsAppDeliveryFailure failure, CancellationToken cancellationToken = default)
    {
        _store.Items[failure.Id] = failure;
        return Task.CompletedTask;
    }
}
