using Api.Shared;
using MediatR;

namespace Api.Features.Authorization;

public sealed record GetUserAssignmentsQuery(Guid UserId) : IQuery<IReadOnlyList<RoleOutput>>;

public sealed class GetUserAssignmentsHandler(
    IUserAssignmentRepository assignmentRepository,
    IRoleRepository roleRepository) : IRequestHandler<GetUserAssignmentsQuery, IReadOnlyList<RoleOutput>>
{
    public async Task<IReadOnlyList<RoleOutput>> Handle(
        GetUserAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var assignments = await assignmentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
        var roles = new List<RoleOutput>();

        foreach (var roleId in roleIds)
        {
            var role = await roleRepository.GetByIdAsync(roleId, cancellationToken);
            if (role is not null)
                roles.Add(role.ToOutput());
        }

        return roles.OrderBy(r => r.Name).ToList();
    }
}

public sealed class GetUserAssignmentsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/authorization/users/{userId:guid}/roles", async (
            Guid userId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetUserAssignmentsQuery(userId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUserAssignments")
        .WithTags("Authorization")
        .RequireAuthorization(SecurityPolicies.AuthorizationRolesRead)
        .Produces<IReadOnlyList<RoleOutput>>(StatusCodes.Status200OK);
    }
}
