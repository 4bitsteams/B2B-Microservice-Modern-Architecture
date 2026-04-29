using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.DeactivateVendor;

public sealed record DeactivateVendorCommand(Guid VendorId) : ICommand<DeactivateVendorResponse>;
public sealed record DeactivateVendorResponse(Guid VendorId, string Status);
