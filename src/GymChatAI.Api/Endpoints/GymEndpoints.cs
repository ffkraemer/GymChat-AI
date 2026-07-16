using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record GymResponse(Guid Id, string Name, string WhatsAppPhoneNumberId, string DefaultLanguage);

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

        return app;
    }
}