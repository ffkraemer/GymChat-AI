using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfCampaignRepository : ICampaignRepository
{
    private readonly GymChatDbContext _context;

    public EfCampaignRepository(GymChatDbContext context) => _context = context;

    public async Task<IReadOnlyList<Campaign>> GetActiveByGymAndTypeAsync(Guid gymId, CampaignType type, CancellationToken cancellationToken = default) =>
        await _context.Campaigns
            .Where(c => c.GymId == gymId && c.Type == type && c.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Campaign>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.Campaigns.Where(c => c.GymId == gymId).ToListAsync(cancellationToken);

    public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Campaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
