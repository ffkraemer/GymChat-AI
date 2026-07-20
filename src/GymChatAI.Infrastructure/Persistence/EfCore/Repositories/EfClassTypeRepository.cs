using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfClassTypeRepository : IClassTypeRepository
{
    private readonly GymChatDbContext _context;

    public EfClassTypeRepository(GymChatDbContext context) => _context = context;

    public Task<ClassType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ClassTypes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClassType>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.ClassTypes.Where(c => c.GymId == gymId && c.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassType>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.ClassTypes.Where(c => c.GymId == gymId).ToListAsync(cancellationToken);

    public async Task AddAsync(ClassType classType, CancellationToken cancellationToken = default)
    {
        _context.ClassTypes.Add(classType);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ClassType classType, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(classType).State == EntityState.Detached)
            _context.ClassTypes.Update(classType);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
