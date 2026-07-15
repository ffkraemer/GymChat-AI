using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfMemberRepository : IMemberRepository
{
    private readonly GymChatDbContext _context;

    public EfMemberRepository(GymChatDbContext context) => _context = context;

    public Task<Member?> GetByPhoneAsync(Guid gymId, string phoneNumber, CancellationToken cancellationToken = default) =>
        _context.Members.FirstOrDefaultAsync(m => m.GymId == gymId && m.PhoneNumber == phoneNumber, cancellationToken);
}
