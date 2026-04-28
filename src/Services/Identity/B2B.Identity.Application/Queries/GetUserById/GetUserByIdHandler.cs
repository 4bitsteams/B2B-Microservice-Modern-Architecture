using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetUsers;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Queries.GetUserById;

public sealed class GetUserByIdHandler(
    IReadUserRepository userRepository,  // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetUserByIdQuery, UserSummaryDto>
{
    public async Task<Result<UserSummaryDto>> Handle(
        GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetWithRolesAsync(request.UserId, cancellationToken);

        if (user is null || user.TenantId != currentUser.TenantId)
            return Error.NotFound("User.NotFound", $"User {request.UserId} not found.");

        return new UserSummaryDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.EmailVerified,
            user.LastLoginAt,
            user.UserRoles.Select(ur => ur.Role.Name).ToList(),
            user.CreatedAt);
    }
}
