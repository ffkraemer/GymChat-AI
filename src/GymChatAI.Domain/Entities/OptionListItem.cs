using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// One option within an OptionList. Value is the stable machine identifier (what gets stored
/// as the answer / matched by the backend); Label is the human-visible text shown in the Flow.
/// Owned exclusively by OptionList - never referenced independently.
/// </summary>
public class OptionListItem : Entity
{
    public Guid OptionListId { get; private set; }
    public string Value { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public int Order { get; private set; }

    private OptionListItem() { }

    public OptionListItem(Guid optionListId, string value, string label, int order)
    {
        OptionListId = optionListId;
        Value = value;
        Label = label;
        Order = order;
    }
}
