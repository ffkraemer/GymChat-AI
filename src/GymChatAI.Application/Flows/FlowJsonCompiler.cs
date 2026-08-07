using System.Text.Json;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Translates the Flow Designer's editable model (FlowScreen/FlowComponent) into Meta's
/// native Flow JSON. The tricky part: WhatsApp Flows don't automatically carry data between
/// screens - each screen only sees what the previous screen's Footer explicitly forwarded to
/// it. So every "navigate" step's payload has to be built here to include every variable
/// collected so far (this screen's own inputs via {form.X}, everything from earlier screens
/// via {data.X}, since that's how it arrives after being forwarded) - otherwise data from
/// screen 1 would be lost by the time the flow reaches its terminal screen.
///
/// isDynamic controls whether "data_api_version" is emitted at all - that property is what
/// tells Meta this flow requires a Data Exchange endpoint. A purely static flow (fixed
/// questions/options only, no dynamic sources) should never declare it, so it never needs an
/// endpoint configured.
///
/// customListItems: the items of every CustomList referenced by a component, pre-resolved by
/// the caller (WhatsAppFlowHandler) and keyed by OptionListId. The compiler stays static and
/// DB-free - it just looks options up in this dictionary when baking a static CustomList
/// source into the JSON. For dynamic flows the endpoint resolves them live instead, so an
/// empty/absent entry here is only a problem for static flows.
/// </summary>
public static class FlowJsonCompiler
{
    public static string Compile(
        IReadOnlyList<FlowScreen> screens,
        bool isDynamic,
        IReadOnlyDictionary<Guid, IReadOnlyList<(string Id, string Title)>>? customListItems = null)
    {
        var orderedScreens = screens.OrderBy(s => s.Order).ToList();

        var routingModel = new Dictionary<string, List<string>>();
        var screenJsons = new List<Dictionary<string, object>>();

        var variablesByScreen = orderedScreens.ToDictionary(
            s => s.ScreenId,
            s => s.Components.Where(IsInputComponent).Select(c => c.VariableName!).ToList());

        var cumulativeBeforeScreen = new List<string>();

        foreach (var screen in orderedScreens)
        {
            var footer = screen.Components.FirstOrDefault(c => c.Type == FlowComponentType.Footer);
            var ownVariables = variablesByScreen[screen.ScreenId];
            var isTerminal = footer?.FooterAction == FlowFooterAction.Complete;

            routingModel[screen.ScreenId] = footer is { FooterAction: FlowFooterAction.Navigate, FooterNextScreenId: not null }
                ? [footer.FooterNextScreenId]
                : [];

            var dataProperties = new Dictionary<string, object>();
            foreach (var carried in cumulativeBeforeScreen)
                dataProperties[carried] = new { type = "string" };

            if (isDynamic)
            {
                foreach (var component in screen.Components.Where(c => IsOptionsComponent(c) && c.OptionsSource != FlowDesignerOptionsSource.Static))
                {
                    dataProperties[$"{component.VariableName}_options"] = new
                    {
                        type = "array",
                        items = new { type = "object", properties = new { id = new { type = "string" }, title = new { type = "string" } } },
                        __example__ = new[] { new { id = "example-id", title = "Exemplo" } }
                    };
                }
            }

            var footerPayload = new Dictionary<string, object>();
            foreach (var name in cumulativeBeforeScreen) footerPayload[name] = $"${{data.{name}}}";
            foreach (var name in ownVariables) footerPayload[name] = $"${{form.{name}}}";

            var children = new List<object>();
            foreach (var component in screen.Components.OrderBy(c => c.Order))
                children.Add(component.Type == FlowComponentType.Footer
                    ? CompileFooter(component, footerPayload)
                    : CompileComponent(component, isDynamic, customListItems));

            screenJsons.Add(new Dictionary<string, object>
            {
                ["id"] = screen.ScreenId,
                ["title"] = screen.Title,
                ["terminal"] = isTerminal,
                ["data"] = dataProperties,
                ["layout"] = new Dictionary<string, object> { ["type"] = "SingleColumnLayout", ["children"] = children }
            });

            if (isTerminal) screenJsons[^1]["success"] = true;

            cumulativeBeforeScreen = cumulativeBeforeScreen.Concat(ownVariables).Distinct().ToList();
        }

        var root = new Dictionary<string, object>
        {
            ["version"] = "7.2",
            ["routing_model"] = routingModel,
            ["screens"] = screenJsons
        };

        if (isDynamic) root["data_api_version"] = "3.0";

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object CompileComponent(
        FlowComponent component, bool isDynamic,
        IReadOnlyDictionary<Guid, IReadOnlyList<(string Id, string Title)>>? customListItems) => component.Type switch
    {
        FlowComponentType.TextHeading => new Dictionary<string, object> { ["type"] = "TextHeading", ["text"] = component.Label },
        FlowComponentType.TextBody => new Dictionary<string, object> { ["type"] = "TextBody", ["text"] = component.Label },

        FlowComponentType.TextInput => new Dictionary<string, object>
        {
            ["type"] = "TextInput",
            ["name"] = component.VariableName!,
            ["label"] = component.Label,
            ["required"] = component.Required
        },

        FlowComponentType.Dropdown or FlowComponentType.CheckboxGroup or FlowComponentType.RadioButtonsGroup => new Dictionary<string, object>
        {
            ["type"] = component.Type.ToString(),
            ["name"] = component.VariableName!,
            ["label"] = component.Label,
            ["required"] = component.Required,
            ["data-source"] = BuildDataSource(component, isDynamic, customListItems)
        },

        _ => throw new InvalidOperationException($"CompileComponent doesn't handle {component.Type} - Footer is compiled separately.")
    };

    private static object BuildDataSource(
        FlowComponent component, bool isDynamic,
        IReadOnlyDictionary<Guid, IReadOnlyList<(string Id, string Title)>>? customListItems)
    {
        // Static flow (or an explicitly Static source): bake a literal array into the JSON.
        if (component.OptionsSource == FlowDesignerOptionsSource.Static || !isDynamic)
        {
            // A CustomList on a static flow: emit the pre-resolved list items as a literal array.
            if (component.OptionsSource == FlowDesignerOptionsSource.CustomList
                && component.OptionListId is Guid listId
                && customListItems is not null
                && customListItems.TryGetValue(listId, out var items))
            {
                return items.Select(i => new Dictionary<string, string> { ["id"] = i.Id, ["title"] = i.Title }).ToList();
            }

            // Static source: whatever literal options were authored.
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(component.StaticOptionsJson ?? "[]") ?? [];
        }

        // Dynamic source (including a dynamic CustomList): reference the "_options" data
        // property that our Data Exchange endpoint populates live at runtime.
        return $"${{data.{component.VariableName}_options}}";
    }

    private static Dictionary<string, object> CompileFooter(FlowComponent footer, Dictionary<string, object> payload)
    {
        if (footer.FooterAction == FlowFooterAction.Complete)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "Footer",
                ["label"] = footer.FooterButtonLabel ?? "Guardar",
                ["on-click-action"] = new Dictionary<string, object> { ["name"] = "complete", ["payload"] = payload }
            };
        }

        return new Dictionary<string, object>
        {
            ["type"] = "Footer",
            ["label"] = footer.FooterButtonLabel ?? "Continuar",
            ["on-click-action"] = new Dictionary<string, object>
            {
                ["name"] = "navigate",
                ["next"] = new Dictionary<string, object> { ["type"] = "screen", ["name"] = footer.FooterNextScreenId ?? "" },
                ["payload"] = payload
            }
        };
    }

    private static bool IsInputComponent(FlowComponent component) =>
        component.Type is FlowComponentType.TextInput or FlowComponentType.Dropdown or FlowComponentType.CheckboxGroup or FlowComponentType.RadioButtonsGroup;

    private static bool IsOptionsComponent(FlowComponent component) =>
        component.Type is FlowComponentType.Dropdown or FlowComponentType.CheckboxGroup or FlowComponentType.RadioButtonsGroup;
}
