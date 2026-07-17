using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record GymResponse(Guid Id, string Name, string WhatsAppPhoneNumberId, string DefaultLanguage);

public record CreateGymRequest(string Name, string WhatsAppPhoneNumberId, string WhatsAppDisplayPhoneNumber, Language? DefaultLanguage);

public static class GymEndpoints
{
    public static IEndpointRouteBuilder MapGymEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/gyms").WithTags("Gyms");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        group.MapGet("/{whatsAppPhoneNumberId}", async (string whatsAppPhoneNumberId, IGymRepository repository, CancellationToken ct) =>
        {
            var gym = await repository.GetByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId, ct);
            return gym is null
                ? Results.NotFound()
                : Results.Ok(new GymResponse(gym.Id, gym.Name, gym.WhatsAppPhoneNumberId, gym.DefaultLanguage.ToString()));
        });

        // Platform-level operations: only PlatformAdmin, never a gym's own Admin - creating
        // gyms is how the platform onboards new clients, not something a client themselves does.
        var listAll = group.MapGet("/", async (IGymRepository repository, CancellationToken ct) =>
        {
            var gyms = await repository.GetAllActiveAsync(ct);
            return Results.Ok(gyms.Select(g => new GymResponse(g.Id, g.Name, g.WhatsAppPhoneNumberId, g.DefaultLanguage.ToString())));
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
            return Results.Created($"/api/gyms/{gym.WhatsAppPhoneNumberId}", new GymResponse(gym.Id, gym.Name, gym.WhatsAppPhoneNumberId, gym.DefaultLanguage.ToString()));
        });
        if (requireAuth) createGym.RequireAuthorization(Policies.PlatformAdmin);

        return app;
    }
}
