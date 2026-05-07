using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.ConfirmOrder;

/// <summary>
/// Command that transitions an order from <c>Pending</c> to <c>Confirmed</c>.
///
/// <para>
/// Typically invoked by an admin or internal system after reviewing a pending order.
/// For orders created via the public API, confirmation happens automatically inside
/// <c>CreateOrderHandler</c>. For basket-checkout orders the
/// <c>BasketCheckedOutConsumer</c> confirms immediately after creation.
/// </para>
///
/// <para>
/// Raises <c>OrderConfirmedEvent</c>, which starts the <c>OrderFulfillmentSaga</c>.
/// </para>
/// </summary>
/// <param name="OrderId">Identifier of the order to confirm.</param>
public sealed record ConfirmOrderCommand(Guid OrderId) : ICommand;
