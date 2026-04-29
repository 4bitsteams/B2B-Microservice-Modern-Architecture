using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Commands.DeactivateVendor;

public sealed class DeactivateVendorHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateVendorCommand, DeactivateVendorResponse>
{
    public async Task<Result<DeactivateVendorResponse>> Handle(DeactivateVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        try { vendor.Deactivate(); }
        catch (InvalidOperationException ex)
        { return Error.Conflict("Vendor.InvalidState", ex.Message); }

        vendorRepository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeactivateVendorResponse(vendor.Id, vendor.Status.ToString());
    }
}
