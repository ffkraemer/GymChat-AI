using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;

namespace GymChatAI.Api.Endpoints;

public record CreateClassTypeRequest(string Name, Guid? GymId = null);

public record UpdateClassTypeRequest(string Name);

public record ClassTypeResponse(Guid Id, string Name, bool IsActive)
{
    public static ClassTypeResponse From(ClassType classType) => new(classType.Id, classType.Name, classType.IsActive);
}

public static class ClassTypeEndpoints
{
    public static IEndpointRouteBuilder MapClassTypeEndpoints(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/class-types").WithTags("Class Types");
        if (requireAuth) group.RequireAuthorization(Policies.Admin);

        var getByGym = group.MapGet("/{gymId:guid}", async (Guid gymId, IClassTypeRepository repository, CancellationToken ct) =>
        {
            var classTypes = await repository.GetAllByGymAsync(gymId, ct);
            return Results.Ok(classTypes.Select(ClassTypeResponse.From));
        });
        if (requireAuth) getByGym.AddEndpointFilter<GymScopeFilter>();

        group.MapPost("/", async (CreateClassTypeRequest request, HttpContext httpContext, IClassTypeRepository repository, CancellationToken ct) =>
        {
            // A regular Admin always gets GymId from their own claim (can never create for
            // another gym). A PlatformAdmin has no "home" gym of their own though - they
            // manage many, so for them the explicit GymId from the request is trusted instead.
            Guid? gymId = httpContext.User.GetGymId();

            if (httpContext.User.IsPlatformAdmin() && request.GymId is not null || !requireAuth)
                gymId = request.GymId;

            if (gymId is null) return Results.BadRequest(new { error = "GymId is required." });

            ClassType classType;
            try
            {
                classType = new ClassType(gymId.Value, request.Name);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            await repository.AddAsync(classType, ct);
            return Results.Created($"/api/class-types/{classType.GymId}", ClassTypeResponse.From(classType));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateClassTypeRequest request, HttpContext httpContext, IClassTypeRepository repository, CancellationToken ct) =>
        {
            var classType = await repository.GetByIdAsync(id, ct);
            if (classType is null) return Results.NotFound();
            if (requireAuth && !IsOwnedByCaller(classType, httpContext)) return Results.Forbid();

            classType.Rename(request.Name);
            await repository.UpdateAsync(classType, ct);
            return Results.Ok(ClassTypeResponse.From(classType));
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, HttpContext httpContext, IClassTypeRepository repository, CancellationToken ct) =>
        {
            var classType = await repository.GetByIdAsync(id, ct);
            if (classType is null) return Results.NotFound();
            if (requireAuth && !IsOwnedByCaller(classType, httpContext)) return Results.Forbid();

            classType.Deactivate();
            await repository.UpdateAsync(classType, ct);
            return Results.Ok(ClassTypeResponse.From(classType));
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, HttpContext httpContext, IClassTypeRepository repository, CancellationToken ct) =>
        {
            var classType = await repository.GetByIdAsync(id, ct);
            if (classType is null) return Results.NotFound();
            if (requireAuth && !IsOwnedByCaller(classType, httpContext)) return Results.Forbid();

            classType.Activate();
            await repository.UpdateAsync(classType, ct);
            return Results.Ok(ClassTypeResponse.From(classType));
        });

        return app;
    }

    private static bool IsOwnedByCaller(ClassType classType, HttpContext httpContext)
    {
        if (httpContext.User.IsPlatformAdmin()) return true;
        return classType.GymId == httpContext.User.GetGymId();
    }
}
