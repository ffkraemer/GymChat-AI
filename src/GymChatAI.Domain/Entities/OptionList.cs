using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// A reusable, admin-managed list of options (e.g. "Objetivos", "Níveis") that can back a
/// Flow's Dropdown/CheckboxGroup/RadioButtonsGroup - replacing what used to be hardcoded
/// option sources. The same list works both statically (its items are baked into the Flow
/// JSON at compile time) and dynamically (resolved live by the Data Exchange endpoint) -
/// which of the two is decided by the Flow's own IsDynamic flag, never by the list itself.
///
/// Scope:
/// - GymId = null  -> a global list, created by a PlatformAdmin, visible to every gym.
/// - GymId set     -> owned by that gym, visible only to it (plus the globals).
///
/// IsSystem lists are the ones migrated from the old hardcoded sources (Dias da semana,
/// Períodos do dia). Their items' Value is locked (only Label/Order can change), because the
/// backend that processes a Flow submission still matches on those exact Values
/// (e.g. "morning"/"afternoon"/"evening", or the day-of-week numbers). Renaming a Value
/// would silently break that mapping - so it isn't allowed for system lists.
/// </summary>
public class OptionList : Entity
{
    private readonly List<OptionListItem> _items = new();

    public Guid? GymId { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Stable machine identifier, unique per scope. For system lists this is a fixed, known
    /// key ("days_of_week", "time_windows") the backend can look up by. For user lists it's
    /// derived from the name at creation but never used for mapping - only Name is shown.
    /// </summary>
    public string Key { get; private set; } = default!;

    /// <summary>True for lists migrated from hardcoded sources - protects item Values from edits/deletes.</summary>
    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<OptionListItem> Items => _items.OrderBy(i => i.Order).ToList().AsReadOnly();

    private OptionList() { }

    public OptionList(Guid? gymId, string name, string key, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Option list name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Option list key is required.", nameof(key));

        GymId = gymId;
        Name = name.Trim();
        Key = key.Trim();
        IsSystem = isSystem;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Option list name is required.", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        // System lists must never be deactivated - Flows and the completion handler rely on them.
        if (IsSystem)
            throw new InvalidOperationException("System option lists cannot be deactivated.");
        IsActive = false;
        Touch();
    }

    public OptionListItem AddItem(string value, string label, int? order = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Item value is required.", nameof(value));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Item label is required.", nameof(label));
        if (_items.Any(i => i.Value == value.Trim()))
            throw new InvalidOperationException($"This list already has an item with value '{value.Trim()}'.");

        var item = new OptionListItem(Id, value.Trim(), label.Trim(), order ?? _items.Count);
        _items.Add(item);
        Touch();
        return item;
    }

    /// <summary>
    /// Replaces the entire item set in one go - how the Portal editor saves. For a system
    /// list, the incoming Values must exactly match the existing ones (only Label/Order may
    /// change); anything else is rejected, to protect the backend mapping.
    /// </summary>
    public void ReplaceItems(IEnumerable<(string Value, string Label, int Order)> newItems)
    {
        var incoming = newItems.ToList();

        if (IsSystem)
        {
            var existingValues = _items.Select(i => i.Value).OrderBy(v => v).ToList();
            var incomingValues = incoming.Select(i => i.Value.Trim()).OrderBy(v => v).ToList();
            if (!existingValues.SequenceEqual(incomingValues))
                throw new InvalidOperationException(
                    "This is a system list - you can rename or reorder its options, but not add, remove, or change their underlying values.");
        }

        _items.Clear();
        foreach (var (value, label, order) in incoming)
            _items.Add(new OptionListItem(Id, value.Trim(), label.Trim(), order));
        Touch();
    }
}
