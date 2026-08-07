using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// One screen of a WhatsApp Flow, as built visually in the Portal's Flow Designer -
/// a container of FlowComponents, always ending with exactly one Footer.
/// </summary>
public class FlowScreen : Entity
{
    private readonly List<FlowComponent> _components = new();
    public Guid WhatsAppFlowId { get; private set; }
    /// <summary>Meta requires a unique, all-caps-with-underscores id per screen within a Flow, e.g. "WELCOME", "PREFERENCES".</summary>
    public string ScreenId { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    /// <summary>Display/navigation order - screens are presented in this sequence by default.</summary>
    public int Order { get; private set; }
    public IReadOnlyCollection<FlowComponent> Components => _components.AsReadOnly();
    private FlowScreen() { }
    public FlowScreen(Guid whatsAppFlowId, string screenId, string title, int order)
    {
        if (string.IsNullOrWhiteSpace(screenId))
            throw new ArgumentException("Screen id is required.", nameof(screenId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Screen title is required.", nameof(title));
        WhatsAppFlowId = whatsAppFlowId;
        // Meta requires screen ids to contain only letters and underscores - no digits, no spaces.
        ScreenId = System.Text.RegularExpressions.Regex.Replace(screenId.ToUpperInvariant(), "[^A-Z]", "_");
        Title = title;
        Order = order;
    }
    public FlowComponent AddComponent(
        Domain.Enums.FlowComponentType type, string label,
        string? variableName = null, bool required = false,
        Domain.Enums.FlowDesignerOptionsSource? optionsSource = null, string? staticOptionsJson = null,
        Domain.Enums.FlowFooterAction? footerAction = null, string? footerNextScreenId = null, string? footerButtonLabel = null,
        Guid? optionListId = null)
    {
        var component = new FlowComponent(
            Id, type, _components.Count, label, variableName, required,
            optionsSource, staticOptionsJson, footerAction, footerNextScreenId, footerButtonLabel, optionListId);
        _components.Add(component);
        return component;
    }
    public void Rename(string title)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title;
        Touch();
    }
}
