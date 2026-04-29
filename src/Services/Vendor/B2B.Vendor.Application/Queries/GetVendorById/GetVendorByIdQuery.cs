using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Queries.GetVendorById;

public sealed record GetVendorByIdQuery(Guid VendorId) : IQuery<VendorDetailDto>;

public sealed record VendorDetailDto(
    Guid Id,
    string CompanyName,
    string ContactEmail,
    string ContactPhone,
    string TaxId,
    string Address,
    string City,
    string Country,
    string Status,
    decimal? CommissionRate,
    string? Website,
    string? Description,
    DateTime CreatedAt);
