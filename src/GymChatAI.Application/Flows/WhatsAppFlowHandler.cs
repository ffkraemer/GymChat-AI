using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

public class WhatsAppFlowHandler
{
    private readonly IWhatsAppFlowRepository _flowRepository;
    private readonly IGymRepository _gymRepository;
    private readonly IWhatsAppFlowManagementClient _managementClient;
    private readonly IOptionListRepository _optionListRepository;

    public WhatsAppFlowHandler(IWhatsAppFlowRepository flowRepository,
        IGymRepository gymRepository,
                        IWhatsAppFlowManagementClient managementClient,
                        IOptionListRepository optionListRepository)
    {
        _flowRepository = flowRepository;
        _gymRepository = gymRepository;
        _managementClient = managementClient;
        _optionListRepository = optionListRepository;
    }

    /// <summary>
    /// Creates the flow on Meta's side and saves it locally in one step, since a Flow needs
    /// a MetaFlowId before its JSON can be uploaded. Starts as a static (non-dynamic) flow
    /// with a single placeholder screen (Meta rejects an empty screens array) - use
    /// ReplaceScreensAsync afterwards, from the Flow Designer, to actually define its
    /// questions and decide whether it needs to be marked dynamic.
    /// </summary>
    public async Task<WhatsAppFlow> CreateAsync(Guid gymId, string name, IReadOnlyList<string> categories, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        if (string.IsNullOrWhiteSpace(gym.WhatsAppBusinessAccountId))
            throw new InvalidOperationException("This gym doesn't have a WhatsApp Business Account id configured yet.");

        var flow = new WhatsAppFlow(gymId, name, "{}", gym.WhatsAppBusinessAccountId);

        var placeholderScreen = new FlowScreen(flow.Id, "SCREEN_ONE", "Ecrã 1", order: 0);
        placeholderScreen.AddComponent(FlowComponentType.TextBody, "Este Flow ainda não tem perguntas configuradas - usa o Flow Designer para as adicionares.");
        placeholderScreen.AddComponent(FlowComponentType.Footer, "Fechar", footerAction: FlowFooterAction.Complete, footerButtonLabel: "Fechar");
        flow.ReplaceScreens([placeholderScreen]);

        var compiledJson = FlowJsonCompiler.Compile(flow.Screens.ToList(), isDynamic: false);
        flow.UpdateFlowJson(compiledJson);

        var createResult = await _managementClient.CreateFlowAsync(gym.WhatsAppBusinessAccountId, name, categories, cancellationToken);
        flow.MarkCreated(createResult.MetaFlowId);
        await _flowRepository.AddAsync(flow, cancellationToken);

        // The Flow JSON has to be uploaded as a separate call, after the Flow itself exists.
        await _managementClient.UpdateFlowJsonAsync(createResult.MetaFlowId, compiledJson, cancellationToken);

        return flow;
    }

    /// <summary>
    /// Deletes a draft flow, on Meta's side first (unlike draft templates, a Flow already
    /// exists on Meta as soon as it's created locally) and then locally. Published flows
    /// can't be deleted at all - only deprecated.
    /// </summary>
    public async Task DeleteDraftAsync(Guid flowId, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.Status != WhatsAppFlowStatus.Draft)
            throw new InvalidOperationException("Only draft flows can be deleted - a published flow can only be deprecated.");

        if (flow.MetaFlowId is not null)
        {
            var deleted = await _managementClient.DeleteFlowAsync(flow.MetaFlowId, cancellationToken);
            if (!deleted)
                throw new InvalidOperationException("Meta refused to delete this flow - it may already be in use by an active session.");
        }

        await _flowRepository.DeleteAsync(flowId, cancellationToken);
    }

    /// <summary>
    /// Flows for a gym, filtered to the gym's *current* WABA - hides records left over from
    /// a WABA the gym has since moved away from, without deleting anything. Records created
    /// before this filtering existed (WhatsAppBusinessAccountId is null) are always shown.
    /// </summary>
    public async Task<IReadOnlyList<WhatsAppFlow>> GetVisibleFlowsAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        var flows = await _flowRepository.GetAllByGymAsync(gymId, cancellationToken);

        return flows
            .Where(f => f.WhatsAppBusinessAccountId is null || f.WhatsAppBusinessAccountId == gym.WhatsAppBusinessAccountId)
            .ToList();
    }

    public async Task PublishAsync(Guid flowId, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        // A dynamic flow is meaningless without somewhere for Meta to fetch live data from -
        // catch this here too, not just client-side, since the endpoint is set via a
        // separate call that could be skipped.
        if (flow.IsDynamic && string.IsNullOrWhiteSpace(flow.EndpointUri))
            throw new InvalidOperationException("This flow is marked as Dynamic and needs a Data Exchange endpoint URL set before it can be published.");

        var (published, errorMessage) = await _managementClient.PublishFlowAsync(flow.MetaFlowId, cancellationToken);
        if (!published)
            throw new InvalidOperationException($"Meta rejected the publish request: {errorMessage}");

        flow.MarkPublished();
        await _flowRepository.UpdateAsync(flow, cancellationToken);
    }

    public async Task RefreshStatusAsync(Guid flowId, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null) return;

        var metaStatus = await _managementClient.GetFlowStatusAsync(flow.MetaFlowId, cancellationToken);
        if (metaStatus is null) return;

        var mapped = metaStatus switch
        {
            "PUBLISHED" => WhatsAppFlowStatus.Published,
            "DEPRECATED" => WhatsAppFlowStatus.Deprecated,
            _ => WhatsAppFlowStatus.Draft
        };

        if (mapped != flow.Status)
        {
            flow.SyncStatus(mapped);
            await _flowRepository.UpdateAsync(flow, cancellationToken);
        }
    }

    /// <summary>One-time per-phone-number setup: registers our RSA public key so Meta can encrypt Data Exchange requests to us.</summary>
    public async Task<bool> RegisterEncryptionKeyAsync(Guid gymId, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        return await _managementClient.RegisterEncryptionKeyAsync(gym.WhatsAppPhoneNumberId, publicKeyPem, cancellationToken);
    }

    /// <summary>
    /// Replaces the whole screen graph - this is how the Flow Designer saves: it always
    /// sends the complete set of screens/components, not incremental diffs. Also updates
    /// whether the flow is marked Dynamic (a purely static flow never declares
    /// "data_api_version", so it never needs an endpoint configured at all).
    /// </summary>
    public async Task<IReadOnlyList<WhatsAppFlowValidationError>> ReplaceScreensAsync(
        Guid flowId, IReadOnlyList<ScreenDefinition> screenDefinitions, bool isDynamic, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");
        if (screenDefinitions.Count == 0)
            throw new InvalidOperationException("A flow needs at least one screen.");

        var screens = new List<FlowScreen>();
        for (var i = 0; i < screenDefinitions.Count; i++)
        {
            var definition = screenDefinitions[i];
            var screen = new FlowScreen(flow.Id, definition.ScreenId, definition.Title, order: i);

            foreach (var component in definition.Components)
            {
                screen.AddComponent(
                    component.Type, component.Label, component.VariableName, component.Required,
                    component.OptionsSource, component.StaticOptionsJson,
                    component.FooterAction, component.FooterNextScreenId, component.FooterButtonLabel,
                    component.OptionListId); ;
            }

            screens.Add(screen);
        }

        flow.ReplaceScreens(screens);
        flow.SetDynamic(isDynamic);

        var customLists = await ResolveCustomListsAsync(flow.Screens, cancellationToken);
        var compiledJson = FlowJsonCompiler.Compile(flow.Screens.ToList(), isDynamic, customLists);

        var result = await _managementClient.UpdateFlowJsonAsync(flow.MetaFlowId, compiledJson, cancellationToken);
        flow.UpdateFlowJson(compiledJson);
        await _flowRepository.UpdateAsync(flow, cancellationToken);

        return result.ValidationErrors;
    }

    /// <summary>
    /// Tells Meta where our Data Exchange endpoint lives for this Flow, and remembers it
    /// locally (so the Portal can show what's already configured instead of an empty field).
    /// </summary>
    public async Task<bool> SetFlowEndpointAsync(Guid flowId, string endpointUri, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        var success = await _managementClient.SetFlowEndpointAsync(flow.MetaFlowId, endpointUri, cancellationToken);
        if (success)
        {
            flow.SetEndpointUri(endpointUri);
            await _flowRepository.UpdateAsync(flow, cancellationToken);
        }

        return success;
    }

    /// <summary>
    /// Uploads a hand-edited/uploaded raw Flow JSON (the JSON editing mode) as-is to Meta,
    /// and stores it verbatim - this is the source of truth sent to Meta, never
    /// recompiled/altered. Also attempts to parse it into the structured Screens model (via
    /// FlowJsonParser), on a best-effort basis, so switching to "Desenho" mode afterwards
    /// shows something sensible to keep editing, instead of being stuck on whatever
    /// structured screens existed before. A parse failure here never blocks the raw JSON
    /// save itself - it's a convenience, not a requirement.
    /// </summary>
    public async Task<IReadOnlyList<WhatsAppFlowValidationError>> UpdateFlowJsonAsync(Guid flowId, string flowJson, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        var result = await _managementClient.UpdateFlowJsonAsync(flow.MetaFlowId, flowJson, cancellationToken);
        flow.UpdateFlowJson(flowJson);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(flowJson);
            flow.SetDynamic(doc.RootElement.TryGetProperty("data_api_version", out _));
        }
        catch
        {
            // Already validated as parseable JSON by the endpoint before calling this - if
            // this still somehow fails, just leave IsDynamic as it was.
        }

        try
        {
            var parsedScreens = FlowJsonParser.Parse(flowJson);
            if (parsedScreens.Count > 0)
            {
                var screens = new List<FlowScreen>();
                for (var i = 0; i < parsedScreens.Count; i++)
                {
                    var definition = parsedScreens[i];
                    var screen = new FlowScreen(flow.Id, definition.ScreenId, definition.Title, order: i);

                    foreach (var component in definition.Components)
                    {
                        screen.AddComponent(
                            component.Type, component.Label, component.VariableName, component.Required,
                            component.OptionsSource, component.StaticOptionsJson,
                            component.FooterAction, component.FooterNextScreenId, component.FooterButtonLabel);
                    }

                    screens.Add(screen);
                }

                flow.ReplaceScreens(screens);
            }
        }
        catch
        {
            // Best-effort only - an unparseable JSON (e.g. hand-written in a shape our
            // parser doesn't recognize) just means Design mode won't reflect it. The raw
            // JSON save above already succeeded regardless.
        }

        await _flowRepository.UpdateAsync(flow, cancellationToken);

        return result.ValidationErrors;
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<(string Id, string Title)>>> ResolveCustomListsAsync(IEnumerable<FlowScreen> screens, CancellationToken cancellationToken)
    {
        var listIds = screens
            .SelectMany(s => s.Components)
            .Where(c => c.OptionsSource == Domain.Enums.FlowDesignerOptionsSource.CustomList && c.OptionListId is not null)
            .Select(c => c.OptionListId!.Value)
            .Distinct()
            .ToList();

        var result = new Dictionary<Guid, IReadOnlyList<(string Id, string Title)>>();
        foreach (var id in listIds)
        {
            var list = await _optionListRepository.GetByIdAsync(id, cancellationToken);
            if (list is not null)
                result[id] = list.Items.Select(i => (i.Value, i.Label)).ToList();
        }
        return result;
    }
}