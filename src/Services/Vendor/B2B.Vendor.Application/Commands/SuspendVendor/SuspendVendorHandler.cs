using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Application.Commands.SuspendVendor;

public sealed class SuspendVendorHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SuspendVendorCommand>
{
    public async Task<Result> Handle(SuspendVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null || vendor.TenantId != currentUser.TenantId)
            return Error.NotFound("Vendor.NotFound", $"Vendor {request.VendorId} not found.");

        try { vendor.Suspend(request.Reason); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Vendor.InvalidStatus", ex.Message); }

        vendorRepository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
