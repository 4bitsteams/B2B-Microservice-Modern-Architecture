using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Queries.GetUsers;

public sealed class GetUsersHandler(
    IReadUserRepository userRepository,  // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetUsersQuery, PagedList<UserSummaryDto>>
{
    public async Task<Result<PagedList<UserSummaryDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetPagedByTenantAsync(
            currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        return users.Map(u => new UserSummaryDto(
            u.Id,
            u.FullName,
            u.Email,
            u.Status.ToString(),
            u.EmailVerified,
            u.LastLoginAt,
            u.UserRoles.Select(ur => ur.Role.Name).ToList(),
            u.CreatedAt));
    }
}
