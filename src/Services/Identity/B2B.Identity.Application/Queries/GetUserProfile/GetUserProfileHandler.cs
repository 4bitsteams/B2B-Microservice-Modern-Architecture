using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetUsers;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Queries.GetUserProfile;

public sealed class GetUserProfileHandler(
    IReadUserRepository userRepository,  // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetUserProfileQuery, UserSummaryDto>
{
    public async Task<Result<UserSummaryDto>> Handle(
        GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetWithRolesAsync(currentUser.UserId, cancellationToken);

        if (user is null)
            return Error.NotFound("User.NotFound", "User profile not found.");

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
