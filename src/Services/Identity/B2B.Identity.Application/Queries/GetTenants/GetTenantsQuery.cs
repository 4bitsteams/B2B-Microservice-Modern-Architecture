using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetTenants;

public sealed record GetTenantsQuery : IQuery<IReadOnlyList<TenantDto>>;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? Domain,
    string Status,
    DateTime CreatedAt);
