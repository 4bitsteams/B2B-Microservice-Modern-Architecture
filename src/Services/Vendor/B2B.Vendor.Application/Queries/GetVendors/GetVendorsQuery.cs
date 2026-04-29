using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Vendor.Application.Queries.GetVendors;

public sealed record GetVendorsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<VendorDto>>;

public sealed record VendorDto(
    Guid Id,
    string CompanyName,
    string ContactEmail,
    string TaxId,
    string City,
    string Country,
    string Status,
    decimal? CommissionRate,
    string? Website,
    DateTime CreatedAt);
