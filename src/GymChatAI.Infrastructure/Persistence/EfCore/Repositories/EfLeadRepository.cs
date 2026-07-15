using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfLeadRepository : ILeadRepository
{
    private readonly GymChatDbContext _context;

    public EfLeadRepository(GymChatDbContext context) => _context = context;

    public Task<Lead?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default) =>
        _context.Leads.FirstOrDefaultAsync(l => l.GymId == gymId && l.PhoneNumber == phoneNumber, cancellationToken);

    public async Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(lead).State == EntityState.Detached)
            _context.Leads.Update(lead);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
