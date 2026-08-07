using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using System.Collections.Concurrent;

namespace GymChatAI.Infrastructure.Persistence;

public class InMemoryWhatsAppFlowStore
{
    public ConcurrentDictionary<Guid, WhatsAppFlow> Items { get; } = new();
}

public class InMemoryWhatsAppFlowRepository : IWhatsAppFlowRepository
{
    private readonly InMemoryWhatsAppFlowStore _store;

    public InMemoryWhatsAppFlowRepository(InMemoryWhatsAppFlowStore store) => _store = store;

    public Task AddAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        _store.Items[flow.Id] = flow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.Items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WhatsAppFlow>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WhatsAppFlow> result = _store.Items.Values.Where(f => f.GymId == gymId).ToList();
        return Task.FromResult(result);
    }

    public Task<WhatsAppFlow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                    Task.FromResult(_store.Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<string>> GetFlowNamesUsingOptionListAsync(Guid optionListId, CancellationToken cancellationToken = default)
    {
        var names = _store.Items.Values
            .Where(f => f.Screens.Any(s => s.Components.Any(c => c.OptionListId == optionListId)))
            .Select(f => f.Name)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public Task UpdateAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        _store.Items[flow.Id] = flow;
        return Task.CompletedTask;
    }
}