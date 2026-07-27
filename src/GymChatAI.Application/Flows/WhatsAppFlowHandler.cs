using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Orchestrates a WhatsApp Flow's lifecycle: create a draft, upload/replace its Flow JSON,
/// publish it, and sync its status back from Meta.
/// </summary>
public class WhatsAppFlowHandler
{
    private readonly IWhatsAppFlowRepository _flowRepository;
    private readonly IGymRepository _gymRepository;
    private readonly IWhatsAppFlowManagementClient _managementClient;

    public WhatsAppFlowHandler(IWhatsAppFlowRepository flowRepository, IGymRepository gymRepository, IWhatsAppFlowManagementClient managementClient)
    {
        _flowRepository = flowRepository;
        _gymRepository = gymRepository;
        _managementClient = managementClient;
    }

    /// <summary>
    /// Creates the flow on Meta's side and saves it locally in one step, since a Flow needs
    /// a MetaFlowId before its JSON can be uploaded. Starts with a single placeholder screen
    /// (Meta rejects an empty screens array) - use ReplaceScreensAsync afterwards, from the
    /// Flow Designer, to actually define its questions.
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

        var compiledJson = FlowJsonCompiler.Compile(flow.Screens.ToList());
        flow.UpdateFlowJson(compiledJson);

        var createResult = await _managementClient.CreateFlowAsync(gym.WhatsAppBusinessAccountId, name, categories, cancellationToken);
        flow.MarkCreated(createResult.MetaFlowId);
        await _flowRepository.AddAsync(flow, cancellationToken);

        // The Flow JSON has to be uploaded as a separate call, after the Flow itself exists.
        await _managementClient.UpdateFlowJsonAsync(createResult.MetaFlowId, compiledJson, cancellationToken);

        return flow;
    }

    /// <summary>
    /// Replaces the whole screen graph - this is how the Flow Designer saves: it always
    /// sends the complete set of screens/components, not incremental diffs. Compiles the
    /// new graph to Meta's Flow JSON and uploads it, so what's on Meta's side always matches
    /// what the Designer shows.
    /// </summary>
    public async Task<IReadOnlyList<WhatsAppFlowValidationError>> ReplaceScreensAsync(
        Guid flowId, IReadOnlyList<ScreenDefinition> screenDefinitions, CancellationToken cancellationToken = default)
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
                    component.FooterAction, component.FooterNextScreenId, component.FooterButtonLabel);
            }

            screens.Add(screen);
        }

        flow.ReplaceScreens(screens);
        var compiledJson = FlowJsonCompiler.Compile(flow.Screens.ToList());

        var result = await _managementClient.UpdateFlowJsonAsync(flow.MetaFlowId, compiledJson, cancellationToken);
        flow.UpdateFlowJson(compiledJson);
        await _flowRepository.UpdateAsync(flow, cancellationToken);

        return result.ValidationErrors;
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

    /// <summary>
    /// Deletes a draft flow, on Meta's side first (unlike draft templates, a Flow already
    /// exists on Meta as soon as it's created locally - CreateAsync above calls CreateFlowAsync
    /// immediately) and then locally. Published flows can't be deleted at all - only deprecated.
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

    public async Task<IReadOnlyList<WhatsAppFlowValidationError>> UpdateFlowJsonAsync(Guid flowId, string flowJson, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        var result = await _managementClient.UpdateFlowJsonAsync(flow.MetaFlowId, flowJson, cancellationToken);
        flow.UpdateFlowJson(flowJson);
        await _flowRepository.UpdateAsync(flow, cancellationToken);

        return result.ValidationErrors;
    }

    public async Task PublishAsync(Guid flowId, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        var (published, errorMessage) = await _managementClient.PublishFlowAsync(flow.MetaFlowId, cancellationToken);
        if (!published)
            throw new InvalidOperationException($"Meta rejected the publish request: {errorMessage}");

        flow.MarkPublished();
        await _flowRepository.UpdateAsync(flow, cancellationToken);
    }

    /// <summary>
    /// Tells Meta where our Data Exchange endpoint lives, for this Flow. Needed because our
    /// preferences form declares dynamic data (data_api_version) - Meta refuses to publish a
    /// dynamic Flow without a reachable endpoint configured. Separate, explicit step (rather
    /// than baked into CreateAsync) since the URL depends on the current ngrok tunnel in
    /// development, and changes independently of the Flow's own lifecycle.
    /// </summary>
    public async Task<bool> SetFlowEndpointAsync(Guid flowId, string endpointUri, CancellationToken cancellationToken = default)
    {
        var flow = await _flowRepository.GetByIdAsync(flowId, cancellationToken)
            ?? throw new InvalidOperationException($"Flow {flowId} not found.");

        if (flow.MetaFlowId is null)
            throw new InvalidOperationException("This flow hasn't been created on Meta's side yet.");

        return await _managementClient.SetFlowEndpointAsync(flow.MetaFlowId, endpointUri, cancellationToken);
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

    /// <summary>
    /// One-time per-phone-number setup: registers our RSA public key so Meta can encrypt
    /// Data Exchange requests to us. Unlike almost every other Flow/Template endpoint (which
    /// are scoped to the WABA), this specific one is scoped to the phone number id - Meta's
    /// own documentation and examples confirm the path is
    /// /{phone-number-id}/whatsapp_business_encryption, not /{waba-id}/...
    /// </summary>
    public async Task<bool> RegisterEncryptionKeyAsync(Guid gymId, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        return await _managementClient.RegisterEncryptionKeyAsync(gym.WhatsAppPhoneNumberId, publicKeyPem, cancellationToken);
    }
}
