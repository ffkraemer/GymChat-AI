using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfCampaignMessageRepository : ICampaignMessageRepository
{
    private readonly GymChatDbContext _context;

    public EfCampaignMessageRepository(GymChatDbContext context) => _context = context;

    public Task<bool> ExistsAsync(Guid campaignId, Guid? memberId, string? periodKey, CancellationToken cancellationToken = default) =>
        _context.CampaignMessages.AnyAsync(
            m => m.CampaignId == campaignId && m.MemberId == memberId && m.PeriodKey == periodKey,
            cancellationToken);

    public async Task<IReadOnlyList<CampaignMessage>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.CampaignMessages
            .Where(m => m.GymId == gymId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default)
    {
        _context.CampaignMessages.Add(campaignMessage);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CampaignMessage campaignMessage, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(campaignMessage).State == EntityState.Detached)
            _context.CampaignMessages.Update(campaignMessage);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
