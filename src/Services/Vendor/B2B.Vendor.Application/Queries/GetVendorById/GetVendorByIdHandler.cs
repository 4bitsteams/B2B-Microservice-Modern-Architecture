using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Queries.GetVendorById;

public sealed class GetVendorByIdHandler(
    IReadVendorRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetVendorByIdQuery, VendorDetailDto>
{
    public async Task<Result<VendorDetailDto>> Handle(GetVendorByIdQuery request, CancellationToken cancellationToken)
    {
        var vendor = await readRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        return new VendorDetailDto(
            vendor.Id, vendor.CompanyName, vendor.ContactEmail,
            vendor.ContactPhone, vendor.TaxId, vendor.Address, vendor.City, vendor.Country,
            vendor.Status.ToString(), vendor.CommissionRate, vendor.Website, vendor.Description,
            vendor.CreatedAt);
    }
}
