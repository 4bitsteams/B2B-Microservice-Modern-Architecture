using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetUsers;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<UserSummaryDto>>;

public sealed record UserSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    bool EmailVerified,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);
