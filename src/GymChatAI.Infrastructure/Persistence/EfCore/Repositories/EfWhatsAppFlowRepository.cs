using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfWhatsAppFlowRepository : IWhatsAppFlowRepository
{
    private readonly GymChatDbContext _context;

    public EfWhatsAppFlowRepository(GymChatDbContext context) => _context = context;

    public async Task AddAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        _context.WhatsAppFlows.Add(flow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flow = await _context.WhatsAppFlows.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null) return;

        _context.WhatsAppFlows.Remove(flow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WhatsAppFlow>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.WhatsAppFlows
            .Include(f => f.Screens)
            .ThenInclude(s => s.Components)
            .Where(f => f.GymId == gymId)
            .ToListAsync(cancellationToken);

    public Task<WhatsAppFlow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                    _context.WhatsAppFlows
            .Include(f => f.Screens)
            .ThenInclude(s => s.Components)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<string>> GetFlowNamesUsingOptionListAsync(Guid optionListId, CancellationToken cancellationToken = default)
    {
        // A flow "uses" the list if any component on any of its screens references it.
        return await _context.WhatsAppFlows
            .Where(f => f.Screens.Any(s => s.Components.Any(c => c.OptionListId == optionListId)))
            .Select(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(WhatsAppFlow flow, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(flow).State == EntityState.Detached)
            _context.WhatsAppFlows.Update(flow);

        await _context.SaveChangesAsync(cancellationToken);
    }
}