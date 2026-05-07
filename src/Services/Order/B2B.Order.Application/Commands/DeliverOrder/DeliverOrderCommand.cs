using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.DeliverOrder;

/// <summary>
/// Command that transitions an order from <c>Shipped</c> to <c>Delivered</c>,
/// recording the delivery timestamp.
///
/// <para>
/// Raises <c>OrderDeliveredEvent</c>, consumed by the Notification Worker to
/// send a delivery-confirmation email to the customer.
/// </para>
///
/// <para>
/// <c>Delivered</c> is a terminal state — no further status transitions are
/// permitted after this command succeeds.
/// </para>
/// </summary>
/// <param name="OrderId">Identifier of the order to mark as delivered.</param>
public sealed record DeliverOrderCommand(Guid OrderId) : ICommand;
