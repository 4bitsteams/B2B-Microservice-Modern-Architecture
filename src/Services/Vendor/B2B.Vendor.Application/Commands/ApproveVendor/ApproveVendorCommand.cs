using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.ApproveVendor;

public sealed record ApproveVendorCommand(Guid VendorId, decimal CommissionRate) : ICommand;
