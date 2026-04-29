using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Queries.GetShipments;

public sealed record GetShipmentsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<ShipmentSummaryDto>>;

public sealed record ShipmentSummaryDto(
    Guid Id,
    Guid OrderId,
    string TrackingNumber,
    string Status,
    string Carrier,
    string RecipientName,
    string? EstimatedDelivery,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime CreatedAt);
