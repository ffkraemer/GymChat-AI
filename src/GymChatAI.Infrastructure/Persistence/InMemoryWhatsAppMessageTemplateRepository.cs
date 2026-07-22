using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence;

public class InMemoryWhatsAppMessageTemplateStore
{
    public ConcurrentDictionary<Guid, WhatsAppMessageTemplate> Items { get; } = new();
}

public class InMemoryWhatsAppMessageTemplateRepository : IWhatsAppMessageTemplateRepository
{
    private readonly InMemoryWhatsAppMessageTemplateStore _store;

    public InMemoryWhatsAppMessageTemplateRepository(InMemoryWhatsAppMessageTemplateStore store) => _store = store;

    public Task<WhatsAppMessageTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<WhatsAppMessageTemplate>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WhatsAppMessageTemplate> result = _store.Items.Values.Where(t => t.GymId == gymId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default)
    {
        _store.Items[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default)
    {
        _store.Items[template.Id] = template;
        return Task.CompletedTask;
    }
}
