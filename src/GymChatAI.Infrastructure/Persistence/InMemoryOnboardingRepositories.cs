using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence;

public class InMemoryClassTypeStore
{
    public ConcurrentDictionary<Guid, ClassType> Items { get; } = new();
}

public class InMemoryClassTypeRepository : IClassTypeRepository
{
    private readonly InMemoryClassTypeStore _store;

    public InMemoryClassTypeRepository(InMemoryClassTypeStore store) => _store = store;

    public Task<ClassType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<ClassType>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClassType> result = _store.Items.Values.Where(c => c.GymId == gymId && c.IsActive).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ClassType>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClassType> result = _store.Items.Values.Where(c => c.GymId == gymId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(ClassType classType, CancellationToken cancellationToken = default)
    {
        _store.Items[classType.Id] = classType;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ClassType classType, CancellationToken cancellationToken = default)
    {
        _store.Items[classType.Id] = classType;
        return Task.CompletedTask;
    }
}

public class InMemoryNotificationPreferenceStore
{
    public ConcurrentDictionary<Guid, NotificationPreference> Items { get; } = new();
}

public class InMemoryNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly InMemoryNotificationPreferenceStore _store;

    public InMemoryNotificationPreferenceRepository(InMemoryNotificationPreferenceStore store) => _store = store;

    public Task<NotificationPreference?> GetByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Items.Values.FirstOrDefault(p => p.GymId == gymId && p.ContactPhoneNumber == contactPhoneNumber));

    public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        _store.Items[preference.Id] = preference;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        _store.Items[preference.Id] = preference;
        return Task.CompletedTask;
    }
}
