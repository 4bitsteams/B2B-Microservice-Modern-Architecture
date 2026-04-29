using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Queries.GetShipments;

public sealed class GetShipmentsHandler(
    IReadShipmentRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetShipmentsQuery, PagedList<ShipmentSummaryDto>>
{
    public async Task<Result<PagedList<ShipmentSummaryDto>>> Handle(GetShipmentsQuery request, CancellationToken cancellationToken)
    {
        var paged = await readRepository.GetPagedByTenantAsync(
            currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        var dtos = paged.Items.Select(s => new ShipmentSummaryDto(
            s.Id, s.OrderId, s.TrackingNumber, s.Status.ToString(),
            s.Carrier, s.RecipientName, s.EstimatedDelivery,
            s.ShippedAt, s.DeliveredAt, s.CreatedAt)).ToList();

        return PagedList<ShipmentSummaryDto>.Create(dtos, request.Page, request.PageSize, paged.TotalCount);
    }
}
