using System.Text.Json;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Parses a raw Flow JSON (as typed/uploaded via the JSON editing mode) back into the
/// structured ScreenDefinition/ComponentDefinition model the Design mode editor uses - the
/// reverse of FlowJsonCompiler. This is what keeps "Desenho" mode showing something sensible
/// after a JSON-mode save, instead of being stuck on whatever was last built structurally.
///
/// IMPORTANT LIMITATION: a dynamic option component's compiled JSON only contains a
/// reference like "${data.selected_classes_options}" - it does NOT record which of our
/// dynamic sources (GymClassTypes/DaysOfWeek/TimeWindows) produced it. There is no way to
/// recover that with certainty from the JSON alone. This parser falls back to a best-effort
/// guess based on the variable name (e.g. containing "class" -> GymClassTypes, "day" ->
/// DaysOfWeek, "window"/"period"/"time" -> TimeWindows, otherwise GymClassTypes as the most
/// common case) - always double-check the "Origem das opções" dropdown after a JSON-mode
/// save that used dynamic data-sources; it may need to be corrected by hand.
/// </summary>
public static class FlowJsonParser
{
    public static IReadOnlyList<ScreenDefinition> Parse(string flowJson)
    {
        using var doc = JsonDocument.Parse(flowJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("screens", out var screensEl) || screensEl.ValueKind != JsonValueKind.Array)
            return [];

        var screens = new List<ScreenDefinition>();

        foreach (var screenEl in screensEl.EnumerateArray())
        {
            var screenId = screenEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "SCREEN" : "SCREEN";
            var title = screenEl.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? screenId : screenId;

            var components = new List<ComponentDefinition>();

            if (screenEl.TryGetProperty("layout", out var layoutEl) &&
                layoutEl.TryGetProperty("children", out var childrenEl) &&
                childrenEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var childEl in childrenEl.EnumerateArray())
                    components.Add(ParseComponent(childEl));
            }

            screens.Add(new ScreenDefinition(screenId, title, components));
        }

        return screens;
    }

    private static ComponentDefinition ParseComponent(JsonElement element)
    {
        var type = element.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        var label = element.TryGetProperty("label", out var labelEl) ? labelEl.GetString()
            : element.TryGetProperty("text", out var textEl) ? textEl.GetString()
            : "";
        var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var required = element.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;

        return type switch
        {
            "TextHeading" => new ComponentDefinition(FlowComponentType.TextHeading, label ?? ""),
            "TextBody" => new ComponentDefinition(FlowComponentType.TextBody, label ?? ""),
            "TextInput" => new ComponentDefinition(FlowComponentType.TextInput, label ?? "", name, required),

            "Dropdown" => ParseOptionsComponent(element, FlowComponentType.Dropdown, label ?? "", name, required),
            "CheckboxGroup" => ParseOptionsComponent(element, FlowComponentType.CheckboxGroup, label ?? "", name, required),
            "RadioButtonsGroup" => ParseOptionsComponent(element, FlowComponentType.RadioButtonsGroup, label ?? "", name, required),

            "Footer" => ParseFooter(element, label ?? ""),

            _ => new ComponentDefinition(FlowComponentType.TextBody, label ?? $"[componente desconhecido: {type}]")
        };
    }

    private static ComponentDefinition ParseOptionsComponent(JsonElement element, FlowComponentType type, string label, string? name, bool required)
    {
        if (!element.TryGetProperty("data-source", out var dataSourceEl))
            return new ComponentDefinition(type, label, name, required, FlowDesignerOptionsSource.Static, "[]");

        if (dataSourceEl.ValueKind == JsonValueKind.Array)
        {
            // A literal, fixed list of options - this is the unambiguous case.
            var options = new List<object>();
            foreach (var optionEl in dataSourceEl.EnumerateArray())
            {
                var id = optionEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : "";
                var title = optionEl.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : id;
                options.Add(new { id, title });
            }

            return new ComponentDefinition(type, label, name, required, FlowDesignerOptionsSource.Static, JsonSerializer.Serialize(options));
        }

        // A "${data.X_options}" reference - a dynamic source, but WHICH one can't be recovered
        // with certainty from the JSON. Best-effort guess from the variable name.
        var guessedSource = GuessDynamicSource(name ?? "");
        return new ComponentDefinition(type, label, name, required, guessedSource, null);
    }

    private static FlowDesignerOptionsSource GuessDynamicSource(string variableName)
    {
        var lower = variableName.ToLowerInvariant();

        if (lower.Contains("class") || lower.Contains("aula")) return FlowDesignerOptionsSource.GymClassTypes;
        if (lower.Contains("day") || lower.Contains("dia")) return FlowDesignerOptionsSource.DaysOfWeek;
        if (lower.Contains("window") || lower.Contains("period") || lower.Contains("time") || lower.Contains("hora"))
            return FlowDesignerOptionsSource.TimeWindows;

        // Most common case in practice - always worth double-checking manually after parsing.
        return FlowDesignerOptionsSource.GymClassTypes;
    }

    private static ComponentDefinition ParseFooter(JsonElement element, string label)
    {
        string? buttonLabel = element.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : label;

        if (!element.TryGetProperty("on-click-action", out var actionEl))
            return new ComponentDefinition(FlowComponentType.Footer, label, FooterAction: FlowFooterAction.Complete, FooterButtonLabel: buttonLabel);

        var actionName = actionEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;

        if (actionName == "navigate")
        {
            string? nextScreenId = null;
            if (actionEl.TryGetProperty("next", out var nextEl) && nextEl.TryGetProperty("name", out var nextNameEl))
                nextScreenId = nextNameEl.GetString();

            return new ComponentDefinition(FlowComponentType.Footer, label, FooterAction: FlowFooterAction.Navigate, FooterNextScreenId: nextScreenId, FooterButtonLabel: buttonLabel);
        }

        return new ComponentDefinition(FlowComponentType.Footer, label, FooterAction: FlowFooterAction.Complete, FooterButtonLabel: buttonLabel);
    }
}
