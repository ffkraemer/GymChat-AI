using System.Text.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Interprets a decrypted Data Exchange request and builds the (still-plaintext) JSON
/// response - the runtime counterpart to FlowJsonCompiler, which prepares the JSON at
/// design time. Handles:
/// - "ping": Meta's periodic health check of the endpoint.
/// - "INIT": the first screen load - returns its dynamic option data (if any).
/// - "data_exchange": a "navigate" Footer was tapped - since FlowJsonCompiler already wires
///   every screen to forward all prior answers via its payload ({form.X}/{data.X}), this
///   just needs to pass that forwarded data through unchanged, adding whatever NEW dynamic
///   option data the next screen itself needs. No server-side session state required - Meta
///   itself carries the accumulated answers on every round trip.
/// The final "complete" submission does NOT come through this endpoint - Meta delivers it as
/// a regular webhook message (interactive.type == "nfm_reply"), handled by
/// WhatsAppFlowCompletionHandler instead.
/// </summary>
public class WhatsAppFlowDataExchangeHandler
{
    private readonly IWhatsAppFlowTokenStore _tokenStore;
    private readonly IWhatsAppFlowRepository _flowRepository;
    private readonly IClassTypeRepository _classTypeRepository;
    private readonly IOptionListRepository _optionListRepository;

    public WhatsAppFlowDataExchangeHandler(
        IWhatsAppFlowTokenStore tokenStore, IWhatsAppFlowRepository flowRepository,
        IClassTypeRepository classTypeRepository, IOptionListRepository optionListRepository)
    {
        _tokenStore = tokenStore;
        _flowRepository = flowRepository;
        _classTypeRepository = classTypeRepository;
        _optionListRepository = optionListRepository;
    }

    public async Task<string> HandleAsync(string decryptedRequestJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(decryptedRequestJson);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() : null;

        if (action == "ping")
            return JsonSerializer.Serialize(new { data = new { status = "active" } });

        var flowToken = root.TryGetProperty("flow_token", out var tokenEl) ? tokenEl.GetString() : null;
        var context = flowToken is not null ? _tokenStore.Resolve(flowToken) : null;
        if (context is null)
            return JsonSerializer.Serialize(new { data = new { acknowledged = true } });

        var flow = await _flowRepository.GetByIdAsync(context.FlowId, cancellationToken);
        if (flow is null)
            return JsonSerializer.Serialize(new { data = new { acknowledged = true } });

        var orderedScreens = flow.Screens.OrderBy(s => s.Order).ToList();

        if (action == "INIT")
        {
            var firstScreen = orderedScreens.FirstOrDefault();
            if (firstScreen is null)
                return JsonSerializer.Serialize(new { data = new { acknowledged = true } });

            var dynamicData = await BuildDynamicOptionsAsync(firstScreen, context.GymId, cancellationToken);
            return JsonSerializer.Serialize(new { screen = firstScreen.ScreenId, data = dynamicData });
        }

        // action == "data_exchange": a Footer with a "navigate" action was tapped.
        var currentScreenId = root.TryGetProperty("screen", out var screenEl) ? screenEl.GetString() : null;
        var currentScreen = orderedScreens.FirstOrDefault(s => s.ScreenId == currentScreenId);
        var nextScreen = currentScreen is null ? null : orderedScreens.FirstOrDefault(s => s.Order == currentScreen.Order + 1);

        if (nextScreen is null)
            return JsonSerializer.Serialize(new { data = new { acknowledged = true } });

        var mergedData = new Dictionary<string, object?>();
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in dataEl.EnumerateObject())
                mergedData[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
        }

        var nextDynamicData = await BuildDynamicOptionsAsync(nextScreen, context.GymId, cancellationToken);
        foreach (var (key, value) in nextDynamicData)
            mergedData[key] = value;

        return JsonSerializer.Serialize(new { screen = nextScreen.ScreenId, data = mergedData });
    }

    /// <summary>Resolves every dynamic-source option component on a screen (GymClassTypes, DaysOfWeek, TimeWindows, or a CustomList) into an actual "{name}_options" data property.</summary>
    private async Task<Dictionary<string, object>> BuildDynamicOptionsAsync(FlowScreen screen, Guid gymId, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object>();

        foreach (var component in screen.Components.Where(IsDynamicOptionsComponent))
        {
            var options = component.OptionsSource switch
            {
                FlowDesignerOptionsSource.GymClassTypes => await BuildClassTypeOptionsAsync(gymId, cancellationToken),
                FlowDesignerOptionsSource.DaysOfWeek => BuildDaysOfWeekOptions(),
                FlowDesignerOptionsSource.TimeWindows => BuildTimeWindowOptions(),
                FlowDesignerOptionsSource.CustomList => await BuildCustomListOptionsAsync(component.OptionListId, cancellationToken),
                _ => new List<object>()
            };

            result[$"{component.VariableName}_options"] = options;
        }

        return result;
    }

    private async Task<List<object>> BuildClassTypeOptionsAsync(Guid gymId, CancellationToken cancellationToken)
    {
        var classTypes = await _classTypeRepository.GetActiveByGymAsync(gymId, cancellationToken);
        return classTypes.Select(c => (object)new { id = c.Id.ToString(), title = c.Name }).ToList();
    }

    /// <summary>Resolves a CustomList (by its OptionListId) into live option data. Inactive/missing list -> empty, so a Flow referencing a since-removed list simply shows no options rather than erroring.</summary>
    private async Task<List<object>> BuildCustomListOptionsAsync(Guid? optionListId, CancellationToken cancellationToken)
    {
        if (optionListId is not Guid id) return new List<object>();

        var list = await _optionListRepository.GetByIdAsync(id, cancellationToken);
        if (list is null || !list.IsActive) return new List<object>();

        return list.Items.Select(i => (object)new { id = i.Value, title = i.Label }).ToList();
    }

    private static List<object> BuildDaysOfWeekOptions() =>
    [
        new { id = ((int)DayOfWeek.Monday).ToString(), title = "Segunda-feira" },
        new { id = ((int)DayOfWeek.Tuesday).ToString(), title = "Terça-feira" },
        new { id = ((int)DayOfWeek.Wednesday).ToString(), title = "Quarta-feira" },
        new { id = ((int)DayOfWeek.Thursday).ToString(), title = "Quinta-feira" },
        new { id = ((int)DayOfWeek.Friday).ToString(), title = "Sexta-feira" },
        new { id = ((int)DayOfWeek.Saturday).ToString(), title = "Sábado" },
        new { id = ((int)DayOfWeek.Sunday).ToString(), title = "Domingo" },
    ];

    private static List<object> BuildTimeWindowOptions() =>
    [
        new { id = "morning", title = "Manhã" },
        new { id = "afternoon", title = "Tarde" },
        new { id = "evening", title = "Noite" },
    ];

    private static bool IsDynamicOptionsComponent(FlowComponent component) =>
        component.Type is FlowComponentType.Dropdown or FlowComponentType.CheckboxGroup or FlowComponentType.RadioButtonsGroup
        && component.OptionsSource is FlowDesignerOptionsSource.GymClassTypes or FlowDesignerOptionsSource.DaysOfWeek
            or FlowDesignerOptionsSource.TimeWindows or FlowDesignerOptionsSource.CustomList;
}
