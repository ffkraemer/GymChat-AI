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

    /// <summary>Creates the flow on Meta's side and saves it locally in one step, since a Flow needs a MetaFlowId before its JSON can be uploaded.</summary>
    public async Task<WhatsAppFlow> CreateAsync(Guid gymId, string name, string flowJson, IReadOnlyList<string> categories, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        if (string.IsNullOrWhiteSpace(gym.WhatsAppBusinessAccountId))
            throw new InvalidOperationException("This gym doesn't have a WhatsApp Business Account id configured yet.");

        var createResult = await _managementClient.CreateFlowAsync(gym.WhatsAppBusinessAccountId, name, categories, cancellationToken);

        var flow = new WhatsAppFlow(gymId, name, flowJson);
        flow.MarkCreated(createResult.MetaFlowId);
        await _flowRepository.AddAsync(flow, cancellationToken);

        // The Flow JSON has to be uploaded as a separate call, after the Flow itself exists.
        await _managementClient.UpdateFlowJsonAsync(createResult.MetaFlowId, flowJson, cancellationToken);

        return flow;
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

        var published = await _managementClient.PublishFlowAsync(flow.MetaFlowId, cancellationToken);
        if (!published)
            throw new InvalidOperationException("Meta rejected the publish request - check the flow's validation errors.");

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
