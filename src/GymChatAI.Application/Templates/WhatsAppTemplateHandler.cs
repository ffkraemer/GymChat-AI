using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Templates;

/// <summary>
/// Lets a gym manage WhatsApp message templates from our own Portal instead of Meta Business
/// Manager: create a draft, submit it for Meta's review, sync back the approval status
/// (including Meta's own category, which can differ from what was submitted), and delete
/// drafts that are no longer wanted.
/// </summary>
public class WhatsAppTemplateHandler
{
    private readonly IWhatsAppMessageTemplateRepository _templateRepository;
    private readonly IGymRepository _gymRepository;
    private readonly IWhatsAppTemplateManagementClient _managementClient;

    public WhatsAppTemplateHandler(
        IWhatsAppMessageTemplateRepository templateRepository,
        IGymRepository gymRepository,
        IWhatsAppTemplateManagementClient managementClient)
    {
        _templateRepository = templateRepository;
        _gymRepository = gymRepository;
        _managementClient = managementClient;
    }

    public async Task<WhatsAppMessageTemplate> CreateDraftAsync(
        Guid gymId, string name, string language, WhatsAppTemplateCategory category, string bodyText, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        var template = new WhatsAppMessageTemplate(gymId, name, language, category, bodyText, gym.WhatsAppBusinessAccountId);
        await _templateRepository.AddAsync(template, cancellationToken);
        return template;
    }

    /// <summary>
    /// Templates for a gym, filtered to the gym's *current* WABA - hides records left over
    /// from a WABA the gym has since moved away from (e.g. switching from a test WABA to
    /// the real one), without deleting anything. Records created before this filtering
    /// existed (WhatsAppBusinessAccountId is null) are always shown, since we can't tell
    /// which WABA they belonged to.
    /// </summary>
    public async Task<IReadOnlyList<WhatsAppMessageTemplate>> GetVisibleTemplatesAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        var templates = await _templateRepository.GetAllByGymAsync(gymId, cancellationToken);

        return templates
            .Where(t => t.WhatsAppBusinessAccountId is null || t.WhatsAppBusinessAccountId == gym.WhatsAppBusinessAccountId)
            .ToList();
    }

    /// <summary>Submits a draft template to Meta for review. Throws if the gym has no WhatsAppBusinessAccountId configured yet.</summary>
    public async Task SubmitForApprovalAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Template {templateId} not found.");

        if (template.Status != WhatsAppTemplateStatus.Draft)
            throw new InvalidOperationException("Only draft templates can be submitted.");

        var gym = await _gymRepository.GetByIdAsync(template.GymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {template.GymId} not found.");

        if (string.IsNullOrWhiteSpace(gym.WhatsAppBusinessAccountId))
            throw new InvalidOperationException(
                "This gym doesn't have a WhatsApp Business Account id configured yet - set it before submitting templates.");

        var variableNames = template.ExtractVariableNames();
        var categoryName = ToMetaCategory(template.Category);

        var result = await _managementClient.SubmitTemplateAsync(
            gym.WhatsAppBusinessAccountId, template.Name, template.Language, categoryName, template.BodyText, variableNames, cancellationToken);

        template.MarkSubmitted(result.MetaTemplateId);
        await _templateRepository.UpdateAsync(template, cancellationToken);
    }

    /// <summary>
    /// Deletes a draft template. Drafts only exist locally (never submitted to Meta, so
    /// there's nothing to clean up on their side) - anything already submitted has to stay,
    /// since Meta's own history/quality tracking depends on it.
    /// </summary>
    public async Task DeleteDraftAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Template {templateId} not found.");

        if (template.Status != WhatsAppTemplateStatus.Draft)
            throw new InvalidOperationException("Only draft templates can be deleted - a submitted template has to stay for Meta's own history/quality tracking.");

        await _templateRepository.DeleteAsync(templateId, cancellationToken);
    }

    /// <summary>Pulls the latest review status (and category) for every submitted template of a gym and updates our local records.</summary>
    public async Task RefreshStatusesAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken)
            ?? throw new InvalidOperationException($"Gym {gymId} not found.");

        if (string.IsNullOrWhiteSpace(gym.WhatsAppBusinessAccountId)) return;

        var remoteStatuses = await _managementClient.GetTemplateStatusesAsync(gym.WhatsAppBusinessAccountId, cancellationToken);
        var remoteByMetaId = remoteStatuses.ToDictionary(s => s.MetaTemplateId);

        var templates = await _templateRepository.GetAllByGymAsync(gymId, cancellationToken);

        foreach (var template in templates)
        {
            if (template.MetaTemplateId is null) continue;
            if (!remoteByMetaId.TryGetValue(template.MetaTemplateId, out var remote)) continue;

            var mappedStatus = FromMetaStatus(remote.Status);
            if (mappedStatus == template.Status && remote.RejectionReason == template.RejectionReason && remote.Category == template.ActualCategory)
                continue;

            template.SyncStatus(mappedStatus, remote.Category, remote.RejectionReason);
            await _templateRepository.UpdateAsync(template, cancellationToken);
        }
    }

    private static string ToMetaCategory(WhatsAppTemplateCategory category) => category switch
    {
        WhatsAppTemplateCategory.Marketing => "MARKETING",
        WhatsAppTemplateCategory.Utility => "UTILITY",
        WhatsAppTemplateCategory.Authentication => "AUTHENTICATION",
        _ => "UTILITY"
    };

    private static WhatsAppTemplateStatus FromMetaStatus(string metaStatus) => metaStatus switch
    {
        "APPROVED" => WhatsAppTemplateStatus.Approved,
        "REJECTED" => WhatsAppTemplateStatus.Rejected,
        "PAUSED" => WhatsAppTemplateStatus.Paused,
        "DISABLED" => WhatsAppTemplateStatus.Disabled,
        _ => WhatsAppTemplateStatus.PendingApproval
    };
}
