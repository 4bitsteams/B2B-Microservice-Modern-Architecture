using B2B.Identity.Application.Queries.GetUsers;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserSummaryDto>;
