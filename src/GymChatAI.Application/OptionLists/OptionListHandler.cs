using System.Text;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.OptionLists;

public record OptionItemInput(string Value, string Label, int Order);

/// <summary>
/// Thrown when a list can't be deleted/deactivated because flows still reference it.
/// Carries the offending flow names so the API can build the "used by «X», «Y»" message.
/// </summary>
public class OptionListInUseException : Exception
{
    public IReadOnlyList<string> FlowNames { get; }

    public OptionListInUseException(IReadOnlyList<string> flowNames)
        : base(BuildMessage(flowNames))
    {
        FlowNames = flowNames;
    }

    private static string BuildMessage(IReadOnlyList<string> flowNames)
    {
        var quoted = string.Join(", ", flowNames.Select(n => $"«{n}»"));
        return $"Não é possível remover: a lista está a ser usada pelo(s) Flow(s): {quoted}.";
    }
}

public class OptionListHandler
{
    private readonly IOptionListRepository _repository;
    private readonly IWhatsAppFlowRepository _flowRepository;

    public OptionListHandler(IOptionListRepository repository, IWhatsAppFlowRepository flowRepository)
    {
        _repository = repository;
        _flowRepository = flowRepository;
    }

    public async Task<OptionList> CreateAsync(Guid? gymId, string name, IReadOnlyList<OptionItemInput> items, CancellationToken cancellationToken = default)
    {
        var key = await MakeUniqueKeyAsync(gymId, name, cancellationToken);
        var list = new OptionList(gymId, name, key, isSystem: false);

        foreach (var item in items.OrderBy(i => i.Order))
            list.AddItem(item.Value, item.Label, item.Order);

        await _repository.AddAsync(list, cancellationToken);
        return list;
    }

    public async Task<OptionList> UpdateAsync(Guid id, string name, IReadOnlyList<OptionItemInput> items, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Option list {id} not found.");

        list.Rename(name);
        list.ReplaceItems(items.Select(i => (i.Value, i.Label, i.Order)));

        await _repository.UpdateAsync(list, cancellationToken);
        return list;
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Option list {id} not found.");

        // Deactivating a list that flows still use would silently break those flows - block it,
        // exactly like deletion. (Reactivating is always fine.)
        if (!active)
        {
            var usedBy = await _flowRepository.GetFlowNamesUsingOptionListAsync(id, cancellationToken);
            if (usedBy.Count > 0)
                throw new OptionListInUseException(usedBy);
        }

        if (active) list.Activate();
        else list.Deactivate(); // also throws for system lists

        await _repository.UpdateAsync(list, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Option list {id} not found.");

        if (list.IsSystem)
            throw new InvalidOperationException("System option lists cannot be deleted.");

        var usedBy = await _flowRepository.GetFlowNamesUsingOptionListAsync(id, cancellationToken);
        if (usedBy.Count > 0)
            throw new OptionListInUseException(usedBy);

        await _repository.DeleteAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<OptionList>> GetVisibleForGymAsync(Guid gymId, bool includeInactive, CancellationToken cancellationToken = default) =>
        _repository.GetVisibleForGymAsync(gymId, includeInactive, cancellationToken);

    public Task<IReadOnlyList<OptionList>> GetGlobalAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        _repository.GetGlobalAsync(includeInactive, cancellationToken);

    private async Task<string> MakeUniqueKeyAsync(Guid? gymId, string name, CancellationToken cancellationToken)
    {
        var baseKey = Slugify(name);
        if (string.IsNullOrEmpty(baseKey)) baseKey = "list";

        var key = baseKey;
        var suffix = 2;
        while (await _repository.GetByKeyAsync(gymId, key, cancellationToken) is not null)
        {
            key = $"{baseKey}_{suffix}";
            suffix++;
        }
        return key;
    }

    private static string Slugify(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('_');
        }
        return sb.ToString()
            .Replace('á', 'a').Replace('à', 'a').Replace('ã', 'a').Replace('â', 'a')
            .Replace('é', 'e').Replace('ê', 'e')
            .Replace('í', 'i')
            .Replace('ó', 'o').Replace('ô', 'o').Replace('õ', 'o')
            .Replace('ú', 'u').Replace('ç', 'c')
            .Trim('_');
    }
}
