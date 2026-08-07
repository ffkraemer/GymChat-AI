using GymChatAI.Domain.Common;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// One field/element on a FlowScreen, as built visually in the Portal's Flow Designer.
/// Translated into Meta's native Flow JSON component syntax by FlowJsonCompiler
/// (Application layer) - this entity is the editable source of truth, not the JSON itself.
/// </summary>
public class FlowComponent : Entity
{
    public Guid FlowScreenId { get; private set; }

    public FlowComponentType Type { get; private set; }

    /// <summary>Display order within the screen.</summary>
    public int Order { get; private set; }

    /// <summary>Heading/body text, or the question label for input components.</summary>
    public string Label { get; private set; } = default!;

    /// <summary>
    /// For input components (TextInput/Dropdown/CheckboxGroup/RadioButtonsGroup): the name
    /// under which the answer appears in the final response_json. Null for
    /// TextHeading/TextBody/Footer, which don't capture input.
    /// </summary>
    public string? VariableName { get; private set; }

    public bool Required { get; private set; }

    /// <summary>Only meaningful for Dropdown/CheckboxGroup/RadioButtonsGroup.</summary>
    public FlowDesignerOptionsSource? OptionsSource { get; private set; }

    /// <summary>Ordered list of {"id":"...","title":"..."} objects, serialized as JSON - only used when OptionsSource is Static.</summary>
    public string? StaticOptionsJson { get; private set; }

    /// <summary>
    /// The reusable OptionList backing this component's options - only set when OptionsSource
    /// is CustomList. The list is resolved at compile time (static flows) or live by the Data
    /// Exchange endpoint (dynamic flows), exactly like the other dynamic sources. A gym can't
    /// delete or deactivate a list while any component still references it here.
    /// </summary>
    public Guid? OptionListId { get; private set; }

    /// <summary>Only meaningful for Footer: what tapping the button does.</summary>
    public FlowFooterAction? FooterAction { get; private set; }

    /// <summary>Only meaningful for a Footer with FooterAction = Navigate: which screen (by ScreenId) to go to next.</summary>
    public string? FooterNextScreenId { get; private set; }

    /// <summary>Footer button text, e.g. "Continuar" or "Guardar".</summary>
    public string? FooterButtonLabel { get; private set; }

    private FlowComponent() { }

    public FlowComponent(
        Guid flowScreenId, FlowComponentType type, int order, string label,
        string? variableName = null, bool required = false,
        FlowDesignerOptionsSource? optionsSource = null, string? staticOptionsJson = null,
        FlowFooterAction? footerAction = null, string? footerNextScreenId = null, string? footerButtonLabel = null,
        Guid? optionListId = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Component label/text is required.", nameof(label));

        FlowScreenId = flowScreenId;
        Type = type;
        Order = order;
        Label = label;
        VariableName = variableName;
        Required = required;
        OptionsSource = optionsSource;
        StaticOptionsJson = staticOptionsJson;
        OptionListId = optionListId;
        FooterAction = footerAction;
        FooterNextScreenId = footerNextScreenId;
        FooterButtonLabel = footerButtonLabel;
    }
}
