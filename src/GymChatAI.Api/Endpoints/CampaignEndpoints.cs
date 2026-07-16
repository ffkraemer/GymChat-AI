using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Loyalty;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateCampaignRequest(string Name, CampaignType Type, string MessageTemplate, int? TriggerDayOffset, Guid? GymId = null);

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
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Loyalty Campaigns");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var byGym = group.MapGet("/gym/{gymId:guid}", async (Guid gymId, ICampaignRepository repository, CancellationToken ct) =>
        {
            var campaigns = await repository.GetByGymAsync(gymId, ct);
            return Results.Ok(campaigns.Select(CampaignResponse.From));
        });
        if (requireAuth) byGym.AddEndpointFilter<GymScopeFilter>();

        group.MapPost("/", async (CreateCampaignRequest request, HttpContext httpContext, ICampaignRepository repository, CancellationToken ct) =>
        {
            var gymId = requireAuth ? httpContext.User.GetGymId() : request.GymId;
            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            Campaign campaign;
            try
            {
                campaign = new Campaign(gymId.Value, request.Name, request.Type, request.MessageTemplate, request.TriggerDayOffset);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            await repository.AddAsync(campaign, ct);
            return Results.Created($"/api/campaigns/gym/{campaign.GymId}", CampaignResponse.From(campaign));
        });

        group.MapPost("/{campaignId:guid}/trigger", async (
            Guid campaignId,
            TriggerManualCampaignRequest request,
            HttpContext httpContext,
            ICampaignRepository campaignRepository,
            LoyaltyEngineHandler loyaltyEngine,
            CancellationToken ct) =>
        {
            if (requireAuth)
            {
                var campaign = await campaignRepository.GetByIdAsync(campaignId, ct);
                var callerGymId = httpContext.User.GetGymId();
                if (campaign is not null && !httpContext.User.IsPlatformAdmin() && campaign.GymId != callerGymId)
                    return Results.Forbid();
            }

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

        var history = group.MapGet("/gym/{gymId:guid}/history", async (Guid gymId, ICampaignMessageRepository repository, CancellationToken ct) =>
        {
            var messages = await repository.GetByGymAsync(gymId, ct);
            return Results.Ok(messages.Select(CampaignMessageResponse.From));
        });
        if (requireAuth) history.AddEndpointFilter<GymScopeFilter>();

        return app;
    }
}