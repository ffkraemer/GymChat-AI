using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfConversationRepository : IConversationRepository
{
    private readonly GymChatDbContext _context;

    public EfConversationRepository(GymChatDbContext context) => _context = context;

    public Task<Conversation?> GetOpenByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default) =>
        _context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.GymId == gymId && c.ContactPhoneNumber == contactPhoneNumber && c.Status != ConversationStatus.Closed)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Conversation>> GetByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.GymId == gymId)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(conversation).State == EntityState.Detached)
            _context.Conversations.Update(conversation);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsByWhatsAppMessageIdAsync(string whatsAppMessageId, CancellationToken cancellationToken = default) =>
        _context.Messages.AnyAsync(m => m.WhatsAppMessageId == whatsAppMessageId, cancellationToken);
}
