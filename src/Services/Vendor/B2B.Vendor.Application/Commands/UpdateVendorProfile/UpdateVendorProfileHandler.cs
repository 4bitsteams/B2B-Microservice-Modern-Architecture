using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Commands.UpdateVendorProfile;

public sealed class UpdateVendorProfileHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateVendorProfileCommand, UpdateVendorProfileResponse>
{
    public async Task<Result<UpdateVendorProfileResponse>> Handle(UpdateVendorProfileCommand request, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        vendor.UpdateProfile(request.CompanyName, request.ContactEmail, request.ContactPhone,
            request.Address, request.City, request.Country, request.Website, request.Description);
        vendorRepository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateVendorProfileResponse(vendor.Id, vendor.CompanyName);
    }
}
