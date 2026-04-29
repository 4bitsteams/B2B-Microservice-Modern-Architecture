using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.ReactivateVendor;

public sealed record ReactivateVendorCommand(Guid VendorId) : ICommand<ReactivateVendorResponse>;
public sealed record ReactivateVendorResponse(Guid VendorId, string Status);
