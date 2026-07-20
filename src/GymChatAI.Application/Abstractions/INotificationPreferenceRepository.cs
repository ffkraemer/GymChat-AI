using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default);

    Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}
