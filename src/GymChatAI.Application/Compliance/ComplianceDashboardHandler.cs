using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

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

    public ComplianceDashboardHandler(IWhatsAppComplianceClient complianceClient, IWhatsAppApiErrorRepository errorRepository)
    {
        _complianceClient = complianceClient;
        _errorRepository = errorRepository;
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

        var riskFlags = BuildRiskFlags(health, errors24h, topErrorCodes);

        return new ComplianceSnapshot(
            health.QualityRating,
            health.MessagingLimit,
            health.NameStatus,
            errors24h.Count,
            errors7d.Count,
            topErrorCodes,
            riskFlags);
    }

    private static List<string> BuildRiskFlags(
        WhatsAppPhoneNumberHealth health,
        IReadOnlyList<WhatsAppApiError> errors24h,
        IReadOnlyList<ErrorCodeBreakdown> topErrorCodes)
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

        // Static, always-shown advisory: our loyalty campaigns currently send free-form text,
        // which WhatsApp only allows within an open 24h customer-service window. Outside that
        // window, Meta requires pre-approved message templates - flagging this here since it's
        // a real compliance gap in the current implementation, not a hypothetical one.
        flags.Add("As campanhas do motor de fidelização (Boas-vindas, Aniversário, Reativação) enviam mensagens de texto livre. Fora da janela de 24h de atendimento, a Meta exige o uso de templates aprovados - conteúdo enviado fora dessa janela sem template está em risco de rejeição ou de penalizar o quality rating.");

        return flags;
    }
}
