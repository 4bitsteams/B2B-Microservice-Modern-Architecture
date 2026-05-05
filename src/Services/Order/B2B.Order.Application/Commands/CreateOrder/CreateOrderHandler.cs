using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Application.Commands.CreateOrder;

public sealed class CreateOrderHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ITaxService taxService,
    IOrderNumberGenerator orderNumberGenerator)
    : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
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

        // A freshly created order is always Pending; propagate defensively.
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
