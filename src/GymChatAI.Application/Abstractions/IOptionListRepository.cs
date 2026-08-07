using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Abstractions;

public interface IOptionListRepository
{
    Task<OptionList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OptionList?> GetByKeyAsync(Guid? gymId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every list visible to a gym: its own lists plus all global (GymId == null) lists.
    /// Pass includeInactive = true for the management page (which shows deactivated ones too).
    /// </summary>
    Task<IReadOnlyList<OptionList>> GetVisibleForGymAsync(Guid gymId, bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>Global lists only (GymId == null) - for the PlatformAdmin management view.</summary>
    Task<IReadOnlyList<OptionList>> GetGlobalAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task AddAsync(OptionList optionList, CancellationToken cancellationToken = default);
    Task UpdateAsync(OptionList optionList, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
