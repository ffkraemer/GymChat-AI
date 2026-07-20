using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly GymChatDbContext _context;

    public EfNotificationPreferenceRepository(GymChatDbContext context) => _context = context;

    public Task<NotificationPreference?> GetByContactAsync(Guid gymId, string contactPhoneNumber, CancellationToken cancellationToken = default) =>
        _context.NotificationPreferences
            .Include(p => p.Slots)
            .FirstOrDefaultAsync(p => p.GymId == gymId && p.ContactPhoneNumber == contactPhoneNumber, cancellationToken);

    public async Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        _context.NotificationPreferences.Add(preference);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(preference).State == EntityState.Detached)
            _context.NotificationPreferences.Update(preference);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
