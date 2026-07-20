using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IClassTypeRepository
{
    Task<ClassType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassType>> GetActiveByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassType>> GetAllByGymAsync(Guid gymId, CancellationToken cancellationToken = default);

    Task AddAsync(ClassType classType, CancellationToken cancellationToken = default);

    Task UpdateAsync(ClassType classType, CancellationToken cancellationToken = default);
}
