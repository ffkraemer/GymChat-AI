using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Application.Compliance;

public record ErrorCodeBreakdown(string? ErrorCode, int Count);

public record ComplianceSnapshot(
    string QualityRating,
    string? MessagingLimit,
    string? NameStatus,
    int ErrorCountLast24h,
    int ErrorCountLast7d,
    IReadOnlyList<ErrorCodeBreakdown> TopErrorCodes,
    IReadOnlyList<string> RiskFlags);

public record MetaDeliveryFailureItem(string WhatsAppMessageId, string RecipientPhoneNumber, string? ErrorCode, string ErrorMessage, DateTimeOffset OccurredAtUtc);

public record ApiCallFailureItem(string Endpoint, int HttpStatusCode, string? ErrorCode, string ErrorMessage, DateTimeOffset OccurredAtUtc);

public record AiFailureItem(Guid ConversationId, string UserMessage, int Attempts, string? LastError, string Status, DateTimeOffset? LastAttemptAtUtc);

public record FailuresSnapshot(
    IReadOnlyList<MetaDeliveryFailureItem> MetaDeliveryFailures,
    IReadOnlyList<ApiCallFailureItem> ApiCallFailures,
    IReadOnlyList<AiFailureItem> AiFailures);

/// <summary>
/// Builds the data behind the Administration Portal's Compliance Dashboard: live quality
/// rating + messaging limit from Meta, our own recent-error history, and a set of risk-flag
/// advisories based on Meta's WhatsApp Business policy (block rates, quality rating,
/// frequency caps, template usage outside the 24h customer service window).
/// </summary>
public class ComplianceDashboardHandler
{
    // Meta's per-user frequency cap for marketing/template messages - see error 131049.
    private const string FrequencyCapErrorCode = "131049";

    private readonly IWhatsAppComplianceClient _complianceClient;
    private readonly IWhatsAppApiErrorRepository _errorRepository;
    private readonly IWhatsAppDeliveryFailureRepository _deliveryFailureRepository;
    private readonly IPendingAIReplyRepository _pendingAIReplyRepository;
    private readonly ICampaignRepository _campaignRepository;
    private readonly IWhatsAppMessageTemplateRepository _templateRepository;

    public ComplianceDashboardHandler(
        IWhatsAppComplianceClient complianceClient,
        IWhatsAppApiErrorRepository errorRepository,
        IWhatsAppDeliveryFailureRepository deliveryFailureRepository,
        IPendingAIReplyRepository pendingAIReplyRepository,
        ICampaignRepository campaignRepository,
        IWhatsAppMessageTemplateRepository templateRepository)
    {
        _complianceClient = complianceClient;
        _errorRepository = errorRepository;
        _deliveryFailureRepository = deliveryFailureRepository;
        _pendingAIReplyRepository = pendingAIReplyRepository;
        _campaignRepository = campaignRepository;
        _templateRepository = templateRepository;
    }

    public async Task<ComplianceSnapshot> GetSnapshotAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        var health = await _complianceClient.GetPhoneNumberHealthAsync(gym.WhatsAppPhoneNumberId, cancellationToken);

        var since24h = DateTimeOffset.UtcNow.AddHours(-24);
        var since7d = DateTimeOffset.UtcNow.AddDays(-7);

        var errors24h = await _errorRepository.GetRecentByGymAsync(gym.Id, since24h, cancellationToken);
        var errors7d = await _errorRepository.GetRecentByGymAsync(gym.Id, since7d, cancellationToken);

        var topErrorCodes = errors7d
            .GroupBy(e => e.ErrorCode)
            .Select(g => new ErrorCodeBreakdown(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var unlinkedCampaignNames = await GetCampaignsMissingApprovedTemplateAsync(gym.Id, cancellationToken);
        var riskFlags = BuildRiskFlags(health, errors24h, topErrorCodes, unlinkedCampaignNames);

        return new ComplianceSnapshot(
            health.QualityRating,
            health.MessagingLimit,
            health.NameStatus,
            errors24h.Count,
            errors7d.Count,
            topErrorCodes,
            riskFlags);
    }

    /// <summary>
    /// Names of active campaigns still sending free text - either never linked to a
    /// WhatsApp template, or linked to one Meta hasn't approved yet. Manual campaigns are
    /// excluded: an operator triggers those by hand, presumably within a window they know
    /// is open (e.g. right after a customer messaged in), so the same risk doesn't apply
    /// automatically the way it does for the always-on automatic campaigns.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetCampaignsMissingApprovedTemplateAsync(Guid gymId, CancellationToken cancellationToken)
    {
        var campaigns = await _campaignRepository.GetByGymAsync(gymId, cancellationToken);
        var automaticActiveCampaigns = campaigns.Where(c => c.IsActive && c.Type != CampaignType.Manual).ToList();

        var missing = new List<string>();
        foreach (var campaign in automaticActiveCampaigns)
        {
            if (campaign.WhatsAppMessageTemplateId is null)
            {
                missing.Add(campaign.Name);
                continue;
            }

            var template = await _templateRepository.GetByIdAsync(campaign.WhatsAppMessageTemplateId.Value, cancellationToken);
            if (template is null || template.Status != WhatsAppTemplateStatus.Approved)
                missing.Add(campaign.Name);
        }

        return missing;
    }

    /// <summary>
    /// The three failure categories the dashboard shows separately:
    /// Meta-reported delivery failures (via the status webhook), our own API call failures,
    /// and AI failures - each tells a different part of the reliability story.
    /// </summary>
    public async Task<FailuresSnapshot> GetFailuresAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        var deliveryFailures = await _deliveryFailureRepository.GetRecentByGymAsync(gymId, since, cancellationToken);
        var apiErrors = await _errorRepository.GetRecentByGymAsync(gymId, since, cancellationToken);
        var aiFailures = await _pendingAIReplyRepository.GetRecentByGymAsync(gymId, since, cancellationToken);

        return new FailuresSnapshot(
            deliveryFailures.Select(f => new MetaDeliveryFailureItem(f.WhatsAppMessageId, f.RecipientPhoneNumber, f.ErrorCode, f.ErrorMessage, f.CreatedAtUtc)).ToList(),
            apiErrors.Select(e => new ApiCallFailureItem(e.Endpoint, e.HttpStatusCode, e.ErrorCode, e.ErrorMessage, e.CreatedAtUtc)).ToList(),
            aiFailures.Select(p => new AiFailureItem(p.ConversationId, p.UserMessage, p.Attempts, p.LastError, p.Status.ToString(), p.LastAttemptAtUtc)).ToList());
    }

    private static List<string> BuildRiskFlags(
        WhatsAppPhoneNumberHealth health,
        IReadOnlyList<WhatsAppApiError> errors24h,
        IReadOnlyList<ErrorCodeBreakdown> topErrorCodes,
        IReadOnlyList<string> campaignsMissingApprovedTemplate)
    {
        var flags = new List<string>();

        switch (health.QualityRating)
        {
            case "RED":
                flags.Add("Quality rating em RED: risco elevado de bloqueio. Tier de mensagens congelado; segue o plano de recuperação da Meta nas próximas 24-48h.");
                break;
            case "YELLOW":
                flags.Add("Quality rating em YELLOW: janela de correção antes de agravar para RED. Revê o conteúdo/segmentação das últimas mensagens enviadas.");
                break;
            case "UNKNOWN" or "NA":
                flags.Add("Quality rating ainda não disponível (número novo ou volume insuficiente para a Meta calcular).");
                break;
        }

        if (topErrorCodes.Any(c => c.ErrorCode == FrequencyCapErrorCode))
        {
            var count = topErrorCodes.First(c => c.ErrorCode == FrequencyCapErrorCode).Count;
            flags.Add($"Detetados {count} erro(s) de limite de frequência por utilizador (código 131049) nos últimos 7 dias - alguns destinatários estão a receber mensagens com demasiada frequência.");
        }

        if (errors24h.Count > 20)
        {
            flags.Add($"Volume elevado de erros da API do WhatsApp nas últimas 24h ({errors24h.Count}).");
        }

        // Only flags the campaigns that are actually still at risk - once a campaign is
        // linked to an Approved template, LoyaltyEngineHandler sends through it and this
        // warning stops mentioning it.
        if (campaignsMissingApprovedTemplate.Count > 0)
        {
            flags.Add(
                $"As campanhas \"{string.Join(", ", campaignsMissingApprovedTemplate)}\" ainda enviam mensagens de texto livre (sem template aprovado ligado). " +
                "Fora da janela de 24h de atendimento, a Meta exige templates aprovados - liga cada campanha a um template Approved na página Templates.");
        }

        return flags;
    }
}
