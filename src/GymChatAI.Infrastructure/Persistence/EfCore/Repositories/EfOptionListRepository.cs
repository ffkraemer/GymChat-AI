using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfOptionListRepository : IOptionListRepository
{
    private readonly GymChatDbContext _context;

    public EfOptionListRepository(GymChatDbContext db) => _context = db;

    public async Task AddAsync(OptionList optionList, CancellationToken cancellationToken = default)
    {
        await _context.OptionLists.AddAsync(optionList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.OptionLists.FindAsync([id], cancellationToken);
        if (existing is not null)
        {
            _context.OptionLists.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<OptionList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                await _context.OptionLists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<OptionList?> GetByKeyAsync(Guid? gymId, string key, CancellationToken cancellationToken = default) =>
        await _context.OptionLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.GymId == gymId && l.Key == key, cancellationToken);

    public async Task<IReadOnlyList<OptionList>> GetGlobalAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _context.OptionLists.Include(l => l.Items).Where(l => l.GymId == null);
        if (!includeInactive) query = query.Where(l => l.IsActive);
        return await query.OrderBy(l => l.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OptionList>> GetVisibleForGymAsync(Guid gymId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _context.OptionLists.Include(l => l.Items).Where(l => l.GymId == gymId || l.GymId == null);
        if (!includeInactive) query = query.Where(l => l.IsActive);
        return await query.OrderBy(l => l.Name).ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(OptionList optionList, CancellationToken cancellationToken = default)
    {
        _context.OptionLists.Update(optionList);
        await _context.SaveChangesAsync(cancellationToken);
    }
}