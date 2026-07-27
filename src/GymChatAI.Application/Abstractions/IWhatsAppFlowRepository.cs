using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IWhatsAppFlowRepository
{
    Task<WhatsAppFlow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsAppFlow>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default);

    Task UpdateAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default);

    /// <summary>Hard delete our local record - the caller is responsible for deleting the flow on Meta's side first, if applicable.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
