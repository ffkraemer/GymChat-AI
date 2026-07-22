using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfGymRepository : IGymRepository
{
    private readonly GymChatDbContext _context;

    public EfGymRepository(GymChatDbContext context) => _context = context;

    public Task<Gym?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Gyms.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<Gym?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default) =>
        _context.Gyms.FirstOrDefaultAsync(g => g.WhatsAppPhoneNumberId == phoneNumberId, cancellationToken);

    public async Task<IReadOnlyList<Gym>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Gyms.Where(g => g.IsActive).ToListAsync(cancellationToken);

    public async Task AddAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        _context.Gyms.Add(gym);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(gym).State == EntityState.Detached)
            _context.Gyms.Update(gym);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
