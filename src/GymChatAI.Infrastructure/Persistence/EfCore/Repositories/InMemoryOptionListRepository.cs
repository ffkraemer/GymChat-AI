using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence.InMemory.Repositories;

public class InMemoryOptionListRepository : IOptionListRepository
{
    private readonly ConcurrentDictionary<Guid, OptionList> _store = new();

    public Task<OptionList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<OptionList?> GetByKeyAsync(Guid? gymId, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(l => l.GymId == gymId && l.Key == key));

    public Task<IReadOnlyList<OptionList>> GetVisibleForGymAsync(Guid gymId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        IEnumerable<OptionList> result = _store.Values.Where(l => l.GymId == gymId || l.GymId == null);
        if (!includeInactive) result = result.Where(l => l.IsActive);
        return Task.FromResult<IReadOnlyList<OptionList>>(result.OrderBy(l => l.Name).ToList());
    }

    public Task<IReadOnlyList<OptionList>> GetGlobalAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        IEnumerable<OptionList> result = _store.Values.Where(l => l.GymId == null);
        if (!includeInactive) result = result.Where(l => l.IsActive);
        return Task.FromResult<IReadOnlyList<OptionList>>(result.OrderBy(l => l.Name).ToList());
    }

    public Task AddAsync(OptionList optionList, CancellationToken cancellationToken = default)
    {
        _store[optionList.Id] = optionList;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OptionList optionList, CancellationToken cancellationToken = default)
    {
        _store[optionList.Id] = optionList;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
