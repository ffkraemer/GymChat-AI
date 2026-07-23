using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfWhatsAppFlowRepository : IWhatsAppFlowRepository
{
    private readonly GymChatDbContext _context;

    public EfWhatsAppFlowRepository(GymChatDbContext context) => _context = context;

    public Task<WhatsAppFlow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.WhatsAppFlows.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WhatsAppFlow>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.WhatsAppFlows.Where(f => f.GymId == gymId).ToListAsync(cancellationToken);

    public async Task AddAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        _context.WhatsAppFlows.Add(flow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(flow).State == EntityState.Detached)
            _context.WhatsAppFlows.Update(flow);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
