using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.RegisterVendor;

public sealed record RegisterVendorCommand(
    string CompanyName,
    string ContactEmail,
    string TaxId,
    string Address,
    string City,
    string Country,
    string? ContactPhone = null,
    string? Website = null,
    string? Description = null) : ICommand<RegisterVendorResponse>;

public sealed record RegisterVendorResponse(Guid VendorId, string CompanyName, string Status);
