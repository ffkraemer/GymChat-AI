using GymChatAI.Application.Abstractions;

namespace GymChatAI.Api.Endpoints;

public record GymResponse(Guid Id, string Name, string WhatsAppPhoneNumberId, string DefaultLanguage);

public static class GymEndpoints
{
    public static IEndpointRouteBuilder MapGymEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gyms/{whatsAppPhoneNumberId}", async (string whatsAppPhoneNumberId, IGymRepository repository, CancellationToken ct) =>
        {
            var gym = await repository.GetByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId, ct);
            return gym is null
                ? Results.NotFound()
                : Results.Ok(new GymResponse(gym.Id, gym.Name, gym.WhatsAppPhoneNumberId, gym.DefaultLanguage.ToString()));
        }).WithTags("Gyms");

        return app;
    }
}
