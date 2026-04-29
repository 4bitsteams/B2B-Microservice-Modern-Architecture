using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Commands.ApproveVendor;

public sealed class ApproveVendorHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveVendorCommand>
{
    public async Task<Result> Handle(ApproveVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        try { vendor.Approve(request.CommissionRate); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Vendor.InvalidStatus", ex.Message); }

        vendorRepository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
