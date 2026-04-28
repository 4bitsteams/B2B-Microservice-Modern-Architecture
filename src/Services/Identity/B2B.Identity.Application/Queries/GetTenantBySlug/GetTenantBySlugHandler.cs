using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetTenants;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Queries.GetTenantBySlug;

public sealed class GetTenantBySlugHandler(IReadTenantRepository tenantRepository)  // read replica — NoTracking
    : IQueryHandler<GetTenantBySlugQuery, TenantDto>
{
    public async Task<Result<TenantDto>> Handle(
        GetTenantBySlugQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.Slug, cancellationToken);

        if (tenant is null)
            return Error.NotFound("Tenant.NotFound", $"Tenant '{request.Slug}' not found.");

        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Domain,
            tenant.Status.ToString(), tenant.CreatedAt);
    }
}
