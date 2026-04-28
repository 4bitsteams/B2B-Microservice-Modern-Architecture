using B2B.Identity.Application.Queries.GetTenants;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetTenantBySlug;

public sealed record GetTenantBySlugQuery(string Slug) : IQuery<TenantDto>;
