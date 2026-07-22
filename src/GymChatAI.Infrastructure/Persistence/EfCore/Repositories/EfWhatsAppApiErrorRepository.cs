using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfWhatsAppApiErrorRepository : IWhatsAppApiErrorRepository
{
    private readonly GymChatDbContext _context;

    public EfWhatsAppApiErrorRepository(GymChatDbContext context) => _context = context;

    public async Task<IReadOnlyList<WhatsAppApiError>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        await _context.WhatsAppApiErrors
            .Where(e => e.GymId == gymId && e.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(WhatsAppApiError error, CancellationToken cancellationToken = default)
    {
        _context.WhatsAppApiErrors.Add(error);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
