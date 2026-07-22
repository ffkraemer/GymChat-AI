using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfPendingAIReplyRepository : IPendingAIReplyRepository
{
    private readonly GymChatDbContext _context;

    public EfPendingAIReplyRepository(GymChatDbContext context) => _context = context;

    public async Task<IReadOnlyList<PendingAIReply>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await _context.PendingAIReplies
            .Where(p => p.Status == PendingAIReplyStatus.Pending)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PendingAIReply>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        await _context.PendingAIReplies
            .Where(p => p.GymId == gymId && p.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default)
    {
        _context.PendingAIReplies.Add(pendingReply);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PendingAIReply pendingReply, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(pendingReply).State == EntityState.Detached)
            _context.PendingAIReplies.Update(pendingReply);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
