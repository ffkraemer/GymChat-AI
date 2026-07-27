using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>Wire-format for one component, as sent by the Flow Designer frontend.</summary>
public record ComponentDefinition(
    FlowComponentType Type,
    string Label,
    string? VariableName = null,
    bool Required = false,
    FlowDesignerOptionsSource? OptionsSource = null,
    string? StaticOptionsJson = null,
    FlowFooterAction? FooterAction = null,
    string? FooterNextScreenId = null,
    string? FooterButtonLabel = null);

/// <summary>Wire-format for one screen, as sent by the Flow Designer frontend.</summary>
public record ScreenDefinition(string ScreenId, string Title, IReadOnlyList<ComponentDefinition> Components);
