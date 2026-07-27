using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IWhatsAppMessageTemplateRepository
{
    Task<WhatsAppMessageTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsAppMessageTemplate>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default);

    Task UpdateAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default);

    /// <summary>Hard delete - only ever called for Draft templates, which have no counterpart on Meta's side yet, so nothing is lost.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
