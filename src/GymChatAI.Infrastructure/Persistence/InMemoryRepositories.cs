using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Infrastructure.Persistence;

/// <summary>
/// Process-memory storage for the POC. Deliberately simple (ConcurrentDictionary-based)
/// so the platform's core messaging flow can be validated end-to-end without provisioning
/// a real database. Swappable for real repositories behind the same interfaces later,
/// once persistence needs to survive a restart.
/// </summary>
public class InMemoryDataStore
{
    public ConcurrentDictionary<Guid, Gym> Gyms { get; } = new();
    public ConcurrentDictionary<Guid, Conversation> Conversations { get; } = new();
    public ConcurrentDictionary<Guid, Faq> Faqs { get; } = new();
    public ConcurrentDictionary<Guid, Lead> Leads { get; } = new();
    public ConcurrentDictionary<Guid, Member> Members { get; } = new();
    public ConcurrentDictionary<Guid, Campaign> Campaigns { get; } = new();
    public ConcurrentDictionary<Guid, CampaignMessage> CampaignMessages { get; } = new();
}

public class InMemoryGymRepository : IGymRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryGymRepository(InMemoryDataStore store) => _store = store;

    public Task<Gym?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Gyms.GetValueOrDefault(id));

    public Task<Gym?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Gyms.Values.FirstOrDefault(g => g.WhatsAppPhoneNumberId == phoneNumberId));

    public Task<IReadOnlyList<Gym>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Gym> result = _store.Gyms.Values.Where(g => g.IsActive).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        _store.Gyms[gym.Id] = gym;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        _store.Gyms[gym.Id] = gym;
        return Task.CompletedTask;
    }
}

public class InMemoryConversationRepository : IConversationRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryConversationRepository(InMemoryDataStore store) => _store = store;

    public Task<Conversation?> GetOpenByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default)
    {
        var conversation = _store.Conversations.Values
            .Where(c => c.GymId == gymId && c.ContactPhoneNumber == contactPhoneNumber)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .FirstOrDefault(c => c.Status != Domain.Enums.ConversationStatus.Closed);

        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Conversations.GetValueOrDefault(id));

    public Task<IReadOnlyList<Conversation>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Conversation> result = _store.Conversations.Values
            .Where(c => c.GymId == gymId)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _store.Conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _store.Conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByWhatsAppMessageIdAsync(string whatsAppMessageId, CancellationToken cancellationToken = default)
    {
        var exists = _store.Conversations.Values
            .SelectMany(c => c.Messages)
            .Any(m => m.WhatsAppMessageId == whatsAppMessageId);

        return Task.FromResult(exists);
    }
}

public class InMemoryFaqRepository : IFaqRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryFaqRepository(InMemoryDataStore store) => _store = store;

    public Task<Faq?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Faqs.GetValueOrDefault(id));

    public Task<IReadOnlyList<Faq>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Faq> result = _store.Faqs.Values.Where(f => f.GymId == gymId && f.IsActive).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Faq>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Faq> result = _store.Faqs.Values.Where(f => f.GymId == gymId).ToList();
        return Task.FromResult(result);
    }

    /// <summary>
    /// POC-grade relevance search: simple case-insensitive keyword overlap.
    /// Replace with embeddings/vector search once the knowledge base grows beyond a POC scale.
    /// </summary>
    public Task<IReadOnlyList<Faq>> SearchAsync(Guid gymId, string query, int maxResults, CancellationToken cancellationToken = default)
    {
        var queryWords = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();

        var result = _store.Faqs.Values
            .Where(f => f.GymId == gymId && f.IsActive)
            .Select(f => (Faq: f, Score: CountOverlap(f.Question, queryWords)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Faq)
            .ToList();

        if (result.Count == 0)
        {
            // Fall back to returning a handful of general FAQs if nothing matched,
            // so the assistant still has some grounding context.
            result = _store.Faqs.Values
                .Where(f => f.GymId == gymId && f.IsActive)
                .Take(maxResults)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<Faq>>(result);
    }

    private static int CountOverlap(string text, HashSet<string> queryWords)
    {
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Count(queryWords.Contains);
    }

    public Task AddAsync(Faq faq, CancellationToken cancellationToken = default)
    {
        _store.Faqs[faq.Id] = faq;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Faq faq, CancellationToken cancellationToken = default)
    {
        _store.Faqs[faq.Id] = faq;
        return Task.CompletedTask;
    }
}

public class InMemoryLeadRepository : ILeadRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryLeadRepository(InMemoryDataStore store) => _store = store;

    public Task<Lead?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Leads.Values.FirstOrDefault(l => l.GymId == gymId && l.PhoneNumber == phoneNumber));

    public Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _store.Leads[lead.Id] = lead;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _store.Leads[lead.Id] = lead;
        return Task.CompletedTask;
    }
}

public class InMemoryMemberRepository : IMemberRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryMemberRepository(InMemoryDataStore store) => _store = store;

    public Task<Member?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Members.Values.FirstOrDefault(m => m.GymId == gymId && m.PhoneNumber == phoneNumber));

    public Task<IReadOnlyList<Member>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Member> result = _store.Members.Values
            .Where(m => m.GymId == gymId && m.Status == Domain.Enums.MemberStatus.Active)
            .ToList();

        return Task.FromResult(result);
    }
}

public class InMemoryCampaignRepository : ICampaignRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryCampaignRepository(InMemoryDataStore store) => _store = store;

    public Task<IReadOnlyList<Campaign>> GetActiveByGymAndTypeAsync(Guid gymId, Domain.Enums.CampaignType type, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Campaign> result = _store.Campaigns.Values
            .Where(c => c.GymId == gymId && c.Type == type && c.IsActive)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Campaign>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Campaign> result = _store.Campaigns.Values.Where(c => c.GymId == gymId).ToList();
        return Task.FromResult(result);
    }

    public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Campaigns.GetValueOrDefault(id));

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _store.Campaigns[campaign.Id] = campaign;
        return Task.CompletedTask;
    }
}

public class InMemoryCampaignMessageRepository : ICampaignMessageRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryCampaignMessageRepository(InMemoryDataStore store) => _store = store;

    public Task<bool> ExistsAsync(Guid campaignId, Guid? memberId, string? periodKey, CancellationToken cancellationToken = default)
    {
        var exists = _store.CampaignMessages.Values.Any(m =>
            m.CampaignId == campaignId && m.MemberId == memberId && m.PeriodKey == periodKey);

        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<CampaignMessage>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CampaignMessage> result = _store.CampaignMessages.Values
            .Where(m => m.GymId == gymId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default)
    {
        _store.CampaignMessages[campaignMessage.Id] = campaignMessage;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default)
    {
        _store.CampaignMessages[campaignMessage.Id] = campaignMessage;
        return Task.CompletedTask;
    }
}
