using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.ShipOrder;

/// <summary>
/// Command that transitions an order from <c>Processing</c> to <c>Shipped</c>
/// and records the carrier tracking number.
///
/// <para>
/// Raises <c>OrderShippedEvent</c>, consumed by the Notification Worker to
/// send a shipment-tracking email to the customer.
/// </para>
/// </summary>
/// <param name="OrderId">Identifier of the order to mark as shipped.</param>
/// <param name="TrackingNumber">
/// Carrier-assigned tracking number (max 200 chars). Stored on the order and
/// included in the shipment-notification email.
/// </param>
public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber) : ICommand;
