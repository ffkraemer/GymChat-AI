using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfWhatsAppDeliveryFailureRepository : IWhatsAppDeliveryFailureRepository
{
    private readonly GymChatDbContext _context;

    public EfWhatsAppDeliveryFailureRepository(GymChatDbContext context) => _context = context;

    public async Task<IReadOnlyList<WhatsAppDeliveryFailure>> GetRecentByGymAsync(Guid gymId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        await _context.WhatsAppDeliveryFailures
            .Where(f => f.GymId == gymId && f.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(WhatsAppDeliveryFailure failure, CancellationToken cancellationToken = default)
    {
        _context.WhatsAppDeliveryFailures.Add(failure);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
