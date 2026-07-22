using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record GymResponse(Guid Id, string Name, string WhatsAppPhoneNumberId, string? WhatsAppBusinessAccountId, string DefaultLanguage)
{
    public static GymResponse From(Gym gym) => new(
        gym.Id, gym.Name, gym.WhatsAppPhoneNumberId, gym.WhatsAppBusinessAccountId, gym.DefaultLanguage.ToString());
}

public record CreateGymRequest(string Name, string WhatsAppPhoneNumberId, string WhatsAppDisplayPhoneNumber, Language? DefaultLanguage);

public record SetWhatsAppBusinessAccountRequest(string WhatsAppBusinessAccountId);

public record SetWhatsAppBusinessAccountResponse(GymResponse Gym, bool WebhookSubscriptionSucceeded);

public static class GymEndpoints
{
    public static IEndpointRouteBuilder MapGymEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/gyms").WithTags("Gyms");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        group.MapGet("/{whatsAppPhoneNumberId}", async (string whatsAppPhoneNumberId, IGymRepository repository, CancellationToken ct) =>
        {
            var gym = await repository.GetByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId, ct);
            return gym is null ? Results.NotFound() : Results.Ok(GymResponse.From(gym));
        });

        // By id (as opposed to the phone-number lookup above) - lets a gym's own Admin fetch
        // their own record, e.g. to check whether a WhatsAppBusinessAccountId is already set
        // before showing an empty "please configure this" form.
        var getById = group.MapGet("/by-id/{gymId:guid}", async (Guid gymId, IGymRepository repository, CancellationToken ct) =>
        {
            var gym = await repository.GetByIdAsync(gymId, ct);
            return gym is null ? Results.NotFound() : Results.Ok(GymResponse.From(gym));
        });
        if (requireAuth) getById.AddEndpointFilter<GymScopeFilter>();

        // Platform-level operations: only PlatformAdmin, never a gym's own Admin - creating
        // gyms is how the platform onboards new clients, not something a client themselves does.
        var listAll = group.MapGet("/", async (IGymRepository repository, CancellationToken ct) =>
        {
            var gyms = await repository.GetAllActiveAsync(ct);
            return Results.Ok(gyms.Select(GymResponse.From));
        });
        if (requireAuth) listAll.RequireAuthorization(Policies.PlatformAdmin);

        var createGym = group.MapPost("/", async (CreateGymRequest request, IGymRepository repository, CancellationToken ct) =>
        {
            Gym gym;
            try
            {
                gym = new Gym(request.Name, request.WhatsAppPhoneNumberId, request.WhatsAppDisplayPhoneNumber, request.DefaultLanguage ?? Language.Portuguese);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            var existing = await repository.GetByWhatsAppPhoneNumberIdAsync(gym.WhatsAppPhoneNumberId, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "A gym with this WhatsApp phone number id already exists." });

            await repository.AddAsync(gym, ct);
            return Results.Created($"/api/gyms/{gym.WhatsAppPhoneNumberId}", GymResponse.From(gym));
        });
        if (requireAuth) createGym.RequireAuthorization(Policies.PlatformAdmin);

        // Needed before templates can be submitted - template management operates at the
        // WABA level, not the phone number level. Also automatically subscribes our App to
        // the WABA's webhook events (POST /{waba-id}/subscribed_apps) - the "missing link"
        // step that otherwise has to be done by hand via Graph API Explorer for every new gym.
        var setWaba = group.MapPost("/{gymId:guid}/whatsapp-business-account", async (
            Guid gymId,
            SetWhatsAppBusinessAccountRequest request,
            IGymRepository repository,
            IWhatsAppWabaAdminClient wabaAdminClient,
            CancellationToken ct) =>
        {
            var gym = await repository.GetByIdAsync(gymId, ct);
            if (gym is null) return Results.NotFound();

            try
            {
                gym.SetWhatsAppBusinessAccountId(request.WhatsAppBusinessAccountId);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            await repository.UpdateAsync(gym, ct);

            var subscribed = await wabaAdminClient.SubscribeAppToWabaAsync(request.WhatsAppBusinessAccountId, ct);
            return Results.Ok(new SetWhatsAppBusinessAccountResponse(GymResponse.From(gym), subscribed));
        });
        if (requireAuth) setWaba.AddEndpointFilter<GymScopeFilter>();

        // Manual retry, in case the automatic subscription above failed (e.g. token
        // permissions weren't ready yet) - avoids needing Graph API Explorer even then.
        var resubscribe = group.MapPost("/{gymId:guid}/resubscribe-webhook", async (
            Guid gymId, IGymRepository repository, IWhatsAppWabaAdminClient wabaAdminClient, CancellationToken ct) =>
        {
            var gym = await repository.GetByIdAsync(gymId, ct);
            if (gym is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(gym.WhatsAppBusinessAccountId))
                return Results.BadRequest(new { error = "This gym has no WhatsApp Business Account id configured yet." });

            var subscribed = await wabaAdminClient.SubscribeAppToWabaAsync(gym.WhatsAppBusinessAccountId, ct);
            return Results.Ok(new { success = subscribed });
        });
        if (requireAuth) resubscribe.AddEndpointFilter<GymScopeFilter>();

        return app;
    }
}
