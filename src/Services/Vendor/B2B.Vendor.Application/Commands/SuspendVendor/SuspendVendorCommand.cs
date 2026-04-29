using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.SuspendVendor;

public sealed record SuspendVendorCommand(Guid VendorId, string Reason) : ICommand;
