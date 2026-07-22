using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IWhatsAppApiErrorRepository
{
    Task<IReadOnlyList<WhatsAppApiError>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    Task AddAsync(WhatsAppApiError error, CancellationToken cancellationToken = default);
}
