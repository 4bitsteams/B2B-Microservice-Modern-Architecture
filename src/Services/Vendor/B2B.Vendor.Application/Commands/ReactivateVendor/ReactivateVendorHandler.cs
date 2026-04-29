using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Commands.ReactivateVendor;

public sealed class ReactivateVendorHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReactivateVendorCommand, ReactivateVendorResponse>
{
    public async Task<Result<ReactivateVendorResponse>> Handle(ReactivateVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        try { vendor.Reactivate(); }
        catch (InvalidOperationException ex)
        { return Error.Conflict("Vendor.InvalidState", ex.Message); }

        vendorRepository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReactivateVendorResponse(vendor.Id, vendor.Status.ToString());
    }
}
