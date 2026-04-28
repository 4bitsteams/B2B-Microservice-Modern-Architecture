using B2B.Identity.Application.Queries.GetUsers;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetUserProfile;

public sealed record GetUserProfileQuery : IQuery<UserSummaryDto>;
