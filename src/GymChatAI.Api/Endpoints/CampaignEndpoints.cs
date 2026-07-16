using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Loyalty;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Api.Endpoints;

public record CreateCampaignRequest(Guid GymId, string Name, CampaignType Type, string MessageTemplate, int? TriggerDayOffset);

public record CampaignResponse(Guid Id, string Name, string Type, string MessageTemplate, int? TriggerDayOffset, bool IsActive)
{
    public static CampaignResponse From(Campaign campaign) => new(
        campaign.Id, campaign.Name, campaign.Type.ToString(), campaign.MessageTemplate, campaign.TriggerDayOffset, campaign.IsActive);
}

public record CampaignMessageResponse(Guid Id, Guid? MemberId, string RecipientPhoneNumber, string Status, DateTimeOffset? SentAtUtc)
{
    public static CampaignMessageResponse From(CampaignMessage message) => new(
        message.Id, message.MemberId, message.RecipientPhoneNumber, message.Status.ToString(), message.SentAtUtc);
}

public record TriggerManualCampaignRequest(List<Guid> MemberIds);

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Loyalty Campaigns");

        group.MapGet("/gym/{gymId:guid}", async (Guid gymId, ICampaignRepository repository, CancellationToken ct) =>
        {
            var campaigns = await repository.GetByGymAsync(gymId, ct);
            return Results.Ok(campaigns.Select(CampaignResponse.From));
        });

        group.MapPost("/", async (CreateCampaignRequest request, ICampaignRepository repository, CancellationToken ct) =>
        {
            Campaign campaign;
            try
            {
                campaign = new Campaign(request.GymId, request.Name, request.Type, request.MessageTemplate, request.TriggerDayOffset);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            await repository.AddAsync(campaign, ct);
            return Results.Created($"/api/campaigns/gym/{campaign.GymId}", CampaignResponse.From(campaign));
        });

        // Sends a Manual campaign immediately to the given members - e.g. "Chuva de Ofertas
        // desta semana" triggered by an operator from the Administration Portal.
        group.MapPost("/{campaignId:guid}/trigger", async (
            Guid campaignId,
            TriggerManualCampaignRequest request,
            LoyaltyEngineHandler loyaltyEngine,
            CancellationToken ct) =>
        {
            try
            {
                var sentCount = await loyaltyEngine.TriggerManualCampaignAsync(campaignId, request.MemberIds, ct);
                return Results.Ok(new { sentCount, requested = request.MemberIds.Count });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapGet("/gym/{gymId:guid}/history", async (Guid gymId, ICampaignMessageRepository repository, CancellationToken ct) =>
        {
            var messages = await repository.GetByGymAsync(gymId, ct);
            return Results.Ok(messages.Select(CampaignMessageResponse.From));
        });

        return app;
    }
}
