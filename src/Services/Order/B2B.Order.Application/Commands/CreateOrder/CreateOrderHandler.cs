using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Application.Commands.CreateOrder;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating an <see cref="OrderEntity"/>
/// aggregate, applying the tenant's tax rate, confirming it immediately, and persisting it.
///
/// <para>
/// Pipeline (executed before this handler):
/// <c>LoggingBehavior → ValidationBehavior (CreateOrderValidator) → IdempotencyBehavior → DomainEventBehavior → Handler</c>
/// </para>
///
/// <para>
/// Design decisions:
/// <list type="bullet">
///   <item>
///     <description>
///       Orders created via the public API are confirmed immediately.
///       This differs from basket-checkout orders (created via <c>BasketCheckedOutConsumer</c>)
///       but the end state is the same: a confirmed order that starts the fulfilment saga.
///     </description>
///   </item>
///   <item>
///     <description>
///       Tax rate is resolved via the pluggable <see cref="ITaxService"/> — injecting a
///       stub in tests produces deterministic totals without external dependencies.
///     </description>
///   </item>
///   <item>
///     <description>
///       Order number generation is delegated to <see cref="IOrderNumberGenerator"/>,
///       keeping the format configurable (sequential, tenant-prefixed, UUID-based, etc.).
///     </description>
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class CreateOrderHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ITaxService taxService,
    IOrderNumberGenerator orderNumberGenerator)
    : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    /// <inheritdoc/>
    public async Task<Result<CreateOrderResponse>> Handle(
        CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var shippingAddress = Address.Create(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country);

        Address? billingAddress = request.BillingAddress is null ? null
            : Address.Create(
                request.BillingAddress.Street,
                request.BillingAddress.City,
                request.BillingAddress.State,
                request.BillingAddress.PostalCode,
                request.BillingAddress.Country);

        var order = OrderEntity.Create(
            currentUser.UserId, currentUser.TenantId,
            shippingAddress,
            orderNumberGenerator.Generate(),
            request.Notes, billingAddress);

        foreach (var item in request.Items)
            order.AddItem(item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity);

        // Resolve tax rate via pluggable service before computing TotalAmount.
        var taxRate = await taxService.GetTaxRateAsync(currentUser.TenantId, ct: cancellationToken);
        order.ApplyTaxRate(taxRate);

        // A freshly created order is always Pending; confirm defensively.
        var confirmResult = order.Confirm();
        if (confirmResult.IsFailure)
            return confirmResult.Error;

        await orderRepository.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResponse(
            order.Id, order.OrderNumber,
            order.TotalAmount, order.Status.ToString());
    }
}
