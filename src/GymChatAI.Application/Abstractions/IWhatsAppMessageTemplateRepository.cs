using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IWhatsAppMessageTemplateRepository
{
    Task<WhatsAppMessageTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsAppMessageTemplate>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default);

    Task UpdateAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default);
}
