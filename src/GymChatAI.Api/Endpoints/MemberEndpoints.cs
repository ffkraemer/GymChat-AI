using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record MemberResponse(Guid Id, string FullName, string PhoneNumber, string Status, DateOnly? BirthDate)
{
    public static MemberResponse From(Member member) => new(
        member.Id, member.FullName, member.PhoneNumber, member.Status.ToString(), member.BirthDate);
}

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/members").WithTags("Members");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var byGym = group.MapGet("/gym/{gymId:guid}", async (Guid gymId, IMemberRepository repository, CancellationToken ct) =>
        {
            var members = await repository.GetActiveByGymAsync(gymId, ct);
            return Results.Ok(members.Select(MemberResponse.From));
        });
        if (requireAuth) byGym.AddEndpointFilter<GymScopeFilter>();

        return app;
    }
}