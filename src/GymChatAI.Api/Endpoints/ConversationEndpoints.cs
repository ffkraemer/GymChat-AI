using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Api.Endpoints;

public record MessageResponse(Guid Id, string Direction, string Origin, string Content, string Status, DateTimeOffset CreatedAtUtc)
{
    public static MessageResponse From(Message message) => new(
        message.Id, message.Direction.ToString(), message.Origin.ToString(), message.Content, message.Status.ToString(), message.CreatedAtUtc);
}

public record ConversationResponse(Guid Id, string ContactPhoneNumber, string Status, string PreferredLanguage, DateTimeOffset LastMessageAtUtc, IReadOnlyList<MessageResponse> Messages)
{
    public static ConversationResponse From(Conversation conversation) => new(
        conversation.Id,
        conversation.ContactPhoneNumber,
        conversation.Status.ToString(),
        conversation.PreferredLanguage.ToString(),
        conversation.LastMessageAtUtc,
        conversation.Messages.Select(MessageResponse.From).ToList());
}

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations").WithTags("Conversations");

        group.MapGet("/gym/{gymId:guid}", async (Guid gymId, IConversationRepository repository, CancellationToken ct) =>
        {
            var conversations = await repository.GetByGymAsync(gymId, ct);
            return Results.Ok(conversations.Select(ConversationResponse.From));
        });

        group.MapGet("/{conversationId:guid}", async (Guid conversationId, IConversationRepository repository, CancellationToken ct) =>
        {
            var conversation = await repository.GetByIdAsync(conversationId, ct);
            return conversation is null ? Results.NotFound() : Results.Ok(ConversationResponse.From(conversation));
        });

        return app;
    }
}
