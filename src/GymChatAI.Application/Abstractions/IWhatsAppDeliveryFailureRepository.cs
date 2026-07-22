using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IWhatsAppDeliveryFailureRepository
{
    Task<IReadOnlyList<WhatsAppDeliveryFailure>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    Task AddAsync(WhatsAppDeliveryFailure failure, CancellationToken cancellationToken = default);
}
