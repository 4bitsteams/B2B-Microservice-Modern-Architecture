using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Application.Commands.RegisterVendor;

public sealed class RegisterVendorHandler(
    IVendorRepository vendorRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterVendorCommand, RegisterVendorResponse>
{
    public async Task<Result<RegisterVendorResponse>> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        var existing = await vendorRepository.GetByEmailAsync(request.ContactEmail, currentUser.TenantId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Vendor.EmailExists", $"A vendor with email '{request.ContactEmail}' already exists.");

        var taxExists = await vendorRepository.GetByTaxIdAsync(request.TaxId, currentUser.TenantId, cancellationToken);
        if (taxExists is not null)
            return Error.Conflict("Vendor.TaxIdExists", $"A vendor with Tax ID '{request.TaxId}' already exists.");

        var vendor = VendorEntity.Register(
            request.CompanyName, request.ContactEmail, request.TaxId,
            request.Address, request.City, request.Country,
            currentUser.TenantId, request.ContactPhone, request.Website, request.Description);

        await vendorRepository.AddAsync(vendor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterVendorResponse(vendor.Id, vendor.CompanyName, vendor.Status.ToString());
    }
}
