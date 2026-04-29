using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Commands.UpdateVendorProfile;

public sealed record UpdateVendorProfileCommand(
    Guid VendorId,
    string CompanyName,
    string ContactEmail,
    string? ContactPhone,
    string Address,
    string City,
    string Country,
    string? Website,
    string? Description) : ICommand<UpdateVendorProfileResponse>;

public sealed record UpdateVendorProfileResponse(Guid VendorId, string CompanyName);
