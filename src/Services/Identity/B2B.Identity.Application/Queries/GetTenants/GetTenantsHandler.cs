using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetTenants;

public sealed class GetTenantsHandler(IReadTenantRepository tenantRepository)  // read replica — NoTracking
    : IQueryHandler<GetTenantsQuery, IReadOnlyList<TenantDto>>
{
    public async Task<Result<IReadOnlyList<TenantDto>>> Handle(
        GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantRepository.FindAsync(_ => true, cancellationToken);

        var dtos = tenants
            .Select(t => new TenantDto(t.Id, t.Name, t.Slug, t.Domain, t.Status.ToString(), t.CreatedAt))
            .ToList();

        return dtos;
    }
}
