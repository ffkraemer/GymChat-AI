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
/// </summary>
public static class FlowJsonCompiler
{
    public static string Compile(IReadOnlyList<FlowScreen> screens)
    {
        var orderedScreens = screens.OrderBy(s => s.Order).ToList();

        var routingModel = new Dictionary<string, List<string>>();
        var screenJsons = new List<Dictionary<string, object>>();

        // Variables each screen owns (its own input components) - used to compute both what
        // gets carried forward into later screens, and this screen's own footer payload.
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

            // "data" schema this screen expects to receive - everything forwarded from earlier
            // screens, plus a generated "_options" property for each of its own dynamic-source
            // input components (populated by our Data Exchange endpoint).
            var dataProperties = new Dictionary<string, object>();
            foreach (var carried in cumulativeBeforeScreen)
                dataProperties[carried] = new { type = "string" };

            foreach (var component in screen.Components.Where(c => IsOptionsComponent(c) && c.OptionsSource != FlowDesignerOptionsSource.Static))
            {
                dataProperties[$"{component.VariableName}_options"] = new
                {
                    type = "array",
                    items = new { type = "object", properties = new { id = new { type = "string" }, title = new { type = "string" } } },
                    __example__ = new[] { new { id = "example-id", title = "Exemplo" } }
                };
            }

            // Every variable collected up to and including this screen - referenced via
            // {form.X} if it's one of THIS screen's own inputs, or {data.X} if it arrived
            // already forwarded from an earlier screen. This is what a Footer (whether
            // navigating onward or completing the flow) sends along.
            var footerPayload = new Dictionary<string, object>();
            foreach (var name in cumulativeBeforeScreen) footerPayload[name] = $"${{data.{name}}}";
            foreach (var name in ownVariables) footerPayload[name] = $"${{form.{name}}}";

            var children = new List<object>();
            foreach (var component in screen.Components.OrderBy(c => c.Order))
                children.Add(component.Type == FlowComponentType.Footer ? CompileFooter(component, footerPayload) : CompileComponent(component));

            var screenJson = new Dictionary<string, object>
            {
                ["id"] = screen.ScreenId,
                ["title"] = screen.Title,
                ["terminal"] = isTerminal,
                ["data"] = dataProperties,
                ["layout"] = new Dictionary<string, object> { ["type"] = "SingleColumnLayout", ["children"] = children }
            };

            // Meta requires at least one terminal screen to explicitly mark 'success' - this
            // is separate from 'terminal' itself (which only says "this ends the flow", not
            // "this end is a successful one"). Every terminal screen we compile represents a
            // completed submission, so it's always a success.
            if (isTerminal) screenJson["success"] = true;

            screenJsons.Add(screenJson);

            cumulativeBeforeScreen = cumulativeBeforeScreen.Concat(ownVariables).Distinct().ToList();
        }

        var root = new Dictionary<string, object>
        {
            ["version"] = "7.2",
            ["data_api_version"] = "3.0",
            ["routing_model"] = routingModel,
            ["screens"] = screenJsons
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object CompileComponent(FlowComponent component) => component.Type switch
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
            ["data-source"] = BuildDataSource(component)
        },

        _ => throw new InvalidOperationException($"CompileComponent doesn't handle {component.Type} - Footer is compiled separately.")
    };

    private static object BuildDataSource(FlowComponent component)
    {
        if (component.OptionsSource == FlowDesignerOptionsSource.Static)
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(component.StaticOptionsJson ?? "[]") ?? [];
        }

        // Dynamic source: reference the "_options" data property populated by our Data Exchange endpoint.
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
