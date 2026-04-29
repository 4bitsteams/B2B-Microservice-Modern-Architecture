using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Vendor.Application.Queries.GetVendors;

public sealed class GetVendorsHandler(
    IReadVendorRepository vendorRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetVendorsQuery, Result<PagedList<VendorDto>>>
{
    public async Task<Result<PagedList<VendorDto>>> Handle(GetVendorsQuery request, CancellationToken cancellationToken)
    {
        var paged = await vendorRepository.GetPagedByTenantAsync(
            currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        var dtos = paged.Items.Select(v => new VendorDto(
            v.Id, v.CompanyName, v.ContactEmail, v.TaxId,
            v.City, v.Country, v.Status.ToString(), v.CommissionRate,
            v.Website, v.CreatedAt)).ToList();

        return PagedList<VendorDto>.Create(dtos, request.Page, request.PageSize, paged.TotalCount);
    }
}
