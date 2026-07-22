using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfWhatsAppMessageTemplateRepository : IWhatsAppMessageTemplateRepository
{
    private readonly GymChatDbContext _context;

    public EfWhatsAppMessageTemplateRepository(GymChatDbContext context) => _context = context;

    public Task<WhatsAppMessageTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.WhatsAppMessageTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WhatsAppMessageTemplate>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.WhatsAppMessageTemplates.Where(t => t.GymId == gymId).ToListAsync(cancellationToken);

    public async Task AddAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default)
    {
        _context.WhatsAppMessageTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WhatsAppMessageTemplate template, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(template).State == EntityState.Detached)
            _context.WhatsAppMessageTemplates.Update(template);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
