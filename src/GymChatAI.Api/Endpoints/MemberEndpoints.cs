using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;

namespace GymChatAI.Api.Endpoints;

public record MemberResponse(Guid Id, string FullName, string PhoneNumber, string Status, DateOnly? BirthDate)
{
    public static MemberResponse From(Member member) => new(
        member.Id, member.FullName, member.PhoneNumber, member.Status.ToString(), member.BirthDate);
}

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/members/gym/{gymId:guid}", async (Guid gymId, IMemberRepository repository, CancellationToken ct) =>
        {
            var members = await repository.GetActiveByGymAsync(gymId, ct);
            return Results.Ok(members.Select(MemberResponse.From));
        }).WithTags("Members");

        return app;
    }
}
