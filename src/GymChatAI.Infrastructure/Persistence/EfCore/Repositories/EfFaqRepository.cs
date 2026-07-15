using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Repositories;

public class EfFaqRepository : IFaqRepository
{
    private readonly GymChatDbContext _context;

    public EfFaqRepository(GymChatDbContext context) => _context = context;

    public async Task<IReadOnlyList<Faq>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default) =>
        await _context.Faqs
            .Where(f => f.GymId == gymId && f.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Faq>> SearchAsync(Guid gymId, string query, int maxResults, CancellationToken cancellationToken = default)
    {
        var queryWords = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();

        var candidates = await _context.Faqs
            .Where(f => f.GymId == gymId && f.IsActive)
            .ToListAsync(cancellationToken);

        var matched = candidates
            .Select(f => (Faq: f, Score: CountOverlap(f.Question, queryWords)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Faq)
            .ToList();

        return matched.Count > 0 ? matched : candidates.Take(maxResults).ToList();
    }

    private static int CountOverlap(string text, HashSet<string> queryWords) =>
        text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(queryWords.Contains);

    public async Task AddAsync(Faq faq, CancellationToken cancellationToken = default)
    {
        _context.Faqs.Add(faq);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
