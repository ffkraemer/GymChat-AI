using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Application.Loyalty;

/// <summary>
/// Evaluates the gym's active loyalty campaigns against its members and dispatches
/// WhatsApp messages for whichever rules match "today". Designed to be called once
/// per gym per day by a scheduler (see LoyaltyEngineBackgroundService in Infrastructure),
/// but every check is independently idempotent via ICampaignMessageRepository, so calling
/// it more often (or replaying a day) never double-sends.
/// </summary>
public class LoyaltyEngineHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignMessageRepository _campaignMessageRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IGymRepository _gymRepository;
    private readonly IWhatsAppMessageSender _whatsAppMessageSender;
    private readonly ILogger<LoyaltyEngineHandler> _logger;

    public LoyaltyEngineHandler(
        ICampaignRepository campaignRepository,
        ICampaignMessageRepository campaignMessageRepository,
        IMemberRepository memberRepository,
        IGymRepository gymRepository,
        IWhatsAppMessageSender whatsAppMessageSender,
        ILogger<LoyaltyEngineHandler> logger)
    {
        _campaignRepository = campaignRepository;
        _campaignMessageRepository = campaignMessageRepository;
        _memberRepository = memberRepository;
        _gymRepository = gymRepository;
        _whatsAppMessageSender = whatsAppMessageSender;
        _logger = logger;
    }

    /// <summary>Runs every automatic campaign type (Welcome, Birthday, Reactivation) for a single gym.</summary>
    public async Task ProcessAutomaticCampaignsForGymAsync(Guid gymId, CancellationToken cancellationToken = default)
    {
        var gym = await _gymRepository.GetByIdAsync(gymId, cancellationToken);
        if (gym is null || !gym.IsActive) return;

        var members = await _memberRepository.GetActiveByGymAsync(gymId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await ProcessWelcomeJourneyAsync(gym, members, today, cancellationToken);
        await ProcessBirthdayMessagesAsync(gym, members, today, cancellationToken);
        await ProcessReactivationAsync(gym, members, cancellationToken);
    }

    private async Task ProcessWelcomeJourneyAsync(Gym gym, IReadOnlyList<Member> members, DateOnly today, CancellationToken cancellationToken)
    {
        var campaigns = await _campaignRepository.GetActiveByGymAndTypeAsync(gym.Id, CampaignType.Welcome, cancellationToken);
        if (campaigns.Count == 0) return;

        foreach (var campaign in campaigns)
        {
            var offset = campaign.TriggerDayOffset!.Value;

            foreach (var member in members)
            {
                // Simplification for the MVP: the member's join date is their CreatedAtUtc.
                var daysSinceJoined = (today.DayNumber - DateOnly.FromDateTime(member.CreatedAtUtc.UtcDateTime).DayNumber);
                if (daysSinceJoined != offset) continue;

                // One-shot campaign: no period key, so it can never fire twice for the same member.
                await DispatchIfNotAlreadySentAsync(campaign, gym, member, periodKey: null, cancellationToken);
            }
        }
    }

    private async Task ProcessBirthdayMessagesAsync(Gym gym, IReadOnlyList<Member> members, DateOnly today, CancellationToken cancellationToken)
    {
        var campaigns = await _campaignRepository.GetActiveByGymAndTypeAsync(gym.Id, CampaignType.Birthday, cancellationToken);
        if (campaigns.Count == 0) return;

        var membersWithBirthdayToday = members.Where(m => m.BirthDate is { } bd && bd.Month == today.Month && bd.Day == today.Day);

        foreach (var campaign in campaigns)
        {
            foreach (var member in membersWithBirthdayToday)
            {
                // Period key = year, so the same member can receive the campaign again next year.
                await DispatchIfNotAlreadySentAsync(campaign, gym, member, periodKey: today.Year.ToString(), cancellationToken);
            }
        }
    }

    private async Task ProcessReactivationAsync(Gym gym, IReadOnlyList<Member> members, CancellationToken cancellationToken)
    {
        var campaigns = await _campaignRepository.GetActiveByGymAndTypeAsync(gym.Id, CampaignType.Reactivation, cancellationToken);
        if (campaigns.Count == 0) return;

        foreach (var campaign in campaigns)
        {
            var threshold = TimeSpan.FromDays(campaign.TriggerDayOffset!.Value);

            foreach (var member in members.Where(m => m.IsInactiveFor(threshold)))
            {
                // Period key tracks the last check-in: if the member comes back and later
                // goes inactive again, this changes, so they become eligible for the
                // reactivation nudge again instead of being permanently excluded.
                var periodKey = member.LastCheckInAtUtc?.ToString("yyyy-MM-dd") ?? "never-checked-in";
                await DispatchIfNotAlreadySentAsync(campaign, gym, member, periodKey, cancellationToken);
            }
        }
    }

    /// <summary>Sends a Manual campaign immediately to an explicit set of members (Administration Portal action).</summary>
    public async Task<int> TriggerManualCampaignAsync(Guid campaignId, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null || !campaign.IsActive)
            throw new InvalidOperationException($"Campaign {campaignId} not found or inactive.");

        var gym = await _gymRepository.GetByIdAsync(campaign.GymId, cancellationToken);
        if (gym is null)
            throw new InvalidOperationException($"Gym {campaign.GymId} not found for campaign {campaignId}.");

        var sentCount = 0;

        foreach (var memberId in memberIds)
        {
            // Manual campaigns are meant to be re-triggerable (e.g. "send this promo again"),
            // so each trigger gets its own period key rather than being deduplicated forever.
            var periodKey = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm");
            var sent = await DispatchToMemberIdAsync(campaign, gym, memberId, periodKey, cancellationToken);
            if (sent) sentCount++;
        }

        return sentCount;
    }

    private async Task<bool> DispatchToMemberIdAsync(Campaign campaign, Gym gym, Guid memberId, string? periodKey, CancellationToken cancellationToken)
    {
        var members = await _memberRepository.GetActiveByGymAsync(gym.Id, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return false;

        return await DispatchIfNotAlreadySentAsync(campaign, gym, member, periodKey, cancellationToken);
    }

    private async Task<bool> DispatchIfNotAlreadySentAsync(Campaign campaign, Gym gym, Member member, string? periodKey, CancellationToken cancellationToken)
    {
        if (await _campaignMessageRepository.ExistsAsync(campaign.Id, member.Id, periodKey, cancellationToken))
            return false;

        var content = MessageTemplateRenderer.Render(campaign.MessageTemplate, member, gym.Name);
        var campaignMessage = new CampaignMessage(campaign.Id, gym.Id, member.PhoneNumber, content, member.Id, periodKey);
        await _campaignMessageRepository.AddAsync(campaignMessage, cancellationToken);

        try
        {
            await _whatsAppMessageSender.SendTextMessageAsync(gym.WhatsAppPhoneNumberId, member.PhoneNumber, content, cancellationToken);
            campaignMessage.MarkSent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send campaign {CampaignId} ({Type}) to member {MemberId}.", campaign.Id, campaign.Type, member.Id);
            campaignMessage.MarkFailed();
        }

        await _campaignMessageRepository.UpdateAsync(campaignMessage, cancellationToken);
        return campaignMessage.Status == CampaignMessageStatus.Sent;
    }
}
