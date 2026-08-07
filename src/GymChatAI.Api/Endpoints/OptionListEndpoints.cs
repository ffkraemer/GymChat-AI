using GymChatAI.Application.Abstractions;
using GymChatAI.Application.OptionLists;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record OptionItemDto(string Value, string Label, int Order)
{
    public static OptionItemDto From(OptionListItem i) => new(i.Value, i.Label, i.Order);
}

public record OptionListResponse(
    Guid Id, Guid? GymId, string Name, string Key, bool IsSystem, bool IsActive, bool IsGlobal, IReadOnlyList<OptionItemDto> Items)
{
    public static OptionListResponse From(OptionList l) => new(
        l.Id, l.GymId, l.Name, l.Key, l.IsSystem, l.IsActive, l.GymId is null,
        l.Items.Select(OptionItemDto.From).ToList());
}

public record CreateOptionListRequest(string Name, IReadOnlyList<OptionItemDto> Items, bool Global = false, Guid? GymId = null);
public record UpdateOptionListRequest(string Name, IReadOnlyList<OptionItemDto> Items);
public record SetOptionListActiveRequest(bool Active);

public static class OptionListEndpoints
{
    public static IEndpointRouteBuilder MapOptionListEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/option-lists").WithTags("Option Lists");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        // Lists visible to a gym: its own + all globals. includeInactive for the management page.
        var getForGym = group.MapGet("/{gymId:guid}", async (
            Guid gymId, bool? includeInactive, OptionListHandler handler, CancellationToken ct) =>
        {
            var lists = await handler.GetVisibleForGymAsync(gymId, includeInactive ?? false, ct);
            return Results.Ok(lists.Select(OptionListResponse.From));
        });
        if (requireAuth) getForGym.AddEndpointFilter<GymScopeFilter>();

        // Global lists only - for the PlatformAdmin management view.
        group.MapGet("/global", async (bool? includeInactive, OptionListHandler handler, CancellationToken ct) =>
        {
            var lists = await handler.GetGlobalAsync(includeInactive ?? false, ct);
            return Results.Ok(lists.Select(OptionListResponse.From));
        }).RequireAuthorization(Policies.PlatformAdmin);

        group.MapPost("/", async (CreateOptionListRequest request, HttpContext http, OptionListHandler handler, CancellationToken ct) =>
        {
            // A global list (GymId == null) can only be created by a PlatformAdmin.
            Guid? targetGymId;
            if (request.Global)
            {
                if (requireAuth && !http.User.IsPlatformAdmin())
                    return Results.Forbid();
                targetGymId = null;
            }
            else
            {
                targetGymId = requireAuth ? http.User.GetGymId() : request.GymId;
                if (targetGymId is null) return Results.BadRequest(new { error = "GymId is required for a non-global list." });
            }

            try
            {
                var items = request.Items.Select(i => new OptionItemInput(i.Value, i.Label, i.Order)).ToList();
                var created = await handler.CreateAsync(targetGymId, request.Name, items, ct);
                return Results.Created($"/api/option-lists/{targetGymId}", OptionListResponse.From(created));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateOptionListRequest request, HttpContext http, OptionListHandler handler, IOptionListRepository repository, CancellationToken ct) =>
        {
            var list = await repository.GetByIdAsync(id, ct);
            if (list is null) return Results.NotFound();
            if (!CanManage(http, list, requireAuth)) return Results.Forbid();

            try
            {
                var items = request.Items.Select(i => new OptionItemInput(i.Value, i.Label, i.Order)).ToList();
                var updated = await handler.UpdateAsync(id, request.Name, items, ct);
                return Results.Ok(OptionListResponse.From(updated));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/active", async (Guid id, SetOptionListActiveRequest request, HttpContext http, OptionListHandler handler, IOptionListRepository repository, CancellationToken ct) =>
        {
            var list = await repository.GetByIdAsync(id, ct);
            if (list is null) return Results.NotFound();
            if (!CanManage(http, list, requireAuth)) return Results.Forbid();

            try
            {
                await handler.SetActiveAsync(id, request.Active, ct);
                return Results.NoContent();
            }
            catch (OptionListInUseException ex)
            {
                return Results.Conflict(new { error = ex.Message, flowNames = ex.FlowNames });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, OptionListHandler handler, IOptionListRepository repository, CancellationToken ct) =>
        {
            var list = await repository.GetByIdAsync(id, ct);
            if (list is null) return Results.NotFound();
            if (!CanManage(http, list, requireAuth)) return Results.Forbid();

            try
            {
                await handler.DeleteAsync(id, ct);
                return Results.NoContent();
            }
            catch (OptionListInUseException ex)
            {
                // 409 Conflict is the right status for "can't do this, it's in use".
                return Results.Conflict(new { error = ex.Message, flowNames = ex.FlowNames });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    /// <summary>
    /// A gym Admin can manage only their own gym's lists. Global lists (GymId == null) can be
    /// managed only by a PlatformAdmin. PlatformAdmin can manage anything.
    /// </summary>
    private static bool CanManage(HttpContext http, OptionList list, bool requireAuth)
    {
        if (!requireAuth) return true;
        if (http.User.IsPlatformAdmin()) return true;
        if (list.GymId is null) return false; // global list, non-platform-admin
        return list.GymId == http.User.GetGymId();
    }
}