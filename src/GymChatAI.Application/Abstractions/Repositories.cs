using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IGymRepository
{
    Task<Gym?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Gym?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default);

    Task AddAsync(Gym gym, CancellationToken cancellationToken = default);
}

public interface IConversationRepository
{
    Task<Conversation?> GetOpenByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default);

    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conversation>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>Idempotency guard: has this WhatsApp message id already been processed?</summary>
    Task<bool> ExistsByWhatsAppMessageIdAsync(string whatsAppMessageId, CancellationToken cancellationToken = default);
}

public interface IFaqRepository
{
    Task<IReadOnlyList<Faq>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Faq>> SearchAsync(Guid gymId, string query, int maxResults, CancellationToken cancellationToken = default);

    Task AddAsync(Faq faq, CancellationToken cancellationToken = default);
}

public interface ILeadRepository
{
    Task<Lead?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default);

    Task AddAsync(Lead lead, CancellationToken cancellationToken = default);

    Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default);
}

public interface IMemberRepository
{
    Task<Member?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default);
}
