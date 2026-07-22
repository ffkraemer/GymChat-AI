using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Abstractions;

public interface IGymRepository
{
    Task<Gym?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Gym?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default);

    /// <summary>All active gyms - used by the loyalty engine scheduler to iterate every tenant.</summary>
    Task<IReadOnlyList<Gym>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Gym gym, CancellationToken cancellationToken = default);

    Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default);
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
    Task<Faq?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Faq>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    /// <summary>Includes inactive FAQs too - used by the Administration Portal so operators can find and reactivate them.</summary>
    Task<IReadOnlyList<Faq>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Faq>> SearchAsync(Guid gymId, string query, int maxResults, CancellationToken cancellationToken = default);

    Task AddAsync(Faq faq, CancellationToken cancellationToken = default);

    Task UpdateAsync(Faq faq, CancellationToken cancellationToken = default);
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

    /// <summary>All active members of a gym - the loyalty engine evaluates its rules against this set.</summary>
    Task<IReadOnlyList<Member>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default);
}

public interface ICampaignRepository
{
    Task<IReadOnlyList<Campaign>> GetActiveByGymAndTypeAsync(Guid gymId, CampaignType type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campaign>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default);
}

public interface ICampaignMessageRepository
{
    /// <summary>Used as the idempotency guard before dispatching a campaign to a member/period.</summary>
    Task<bool> ExistsAsync(Guid campaignId, Guid? memberId, string? periodKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignMessage>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default);

    Task UpdateAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default);
}
