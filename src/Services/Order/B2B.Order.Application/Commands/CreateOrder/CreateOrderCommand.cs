using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Commands.CreateOrder;

/// <summary>
/// Command that creates a new order for the currently authenticated customer.
///
/// <para>
/// Implements <see cref="IIdempotentCommand"/> so that duplicate submissions
/// (network retries, double-clicks) are short-circuited by <c>IdempotencyBehavior</c>
/// using the client-supplied <see cref="IdempotencyKey"/>.
/// </para>
/// </summary>
/// <param name="ShippingAddress">Required destination address for delivery.</param>
/// <param name="BillingAddress">Optional billing address; defaults to shipping address when omitted.</param>
/// <param name="Items">At least one line-item must be provided.</param>
/// <param name="Notes">Optional free-text instructions for the fulfilment team.</param>
public sealed record CreateOrderCommand(
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    IReadOnlyList<OrderItemRequest> Items,
    string? Notes = null) : ICommand<CreateOrderResponse>, IIdempotentCommand
{
    /// <inheritdoc/>
    public string IdempotencyKey { get; init; } = string.Empty;
}

/// <summary>
/// Address data-transfer object used in <see cref="CreateOrderCommand"/>.
/// Maps 1-to-1 to the <c>Address</c> value object created inside the handler.
/// </summary>
/// <param name="Street">Street line including building number.</param>
/// <param name="City">City or locality.</param>
/// <param name="State">State, province, or region (may be empty).</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code (e.g. "US").</param>
public sealed record AddressDto(
    string Street, string City, string State,
    string PostalCode, string Country);

/// <summary>
/// A single line-item within a <see cref="CreateOrderCommand"/>.
/// Product details are snapshotted at order time so the order record is
/// immutable against future catalogue changes.
/// </summary>
/// <param name="ProductId">Product being ordered.</param>
/// <param name="ProductName">Display name snapshot.</param>
/// <param name="Sku">SKU snapshot.</param>
/// <param name="UnitPrice">Price per unit at order time — must be ≥ 0.</param>
/// <param name="Quantity">Number of units — must be &gt; 0.</param>
public sealed record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);

/// <summary>
/// Returned by <see cref="CreateOrderHandler"/> on success.
/// Gives the client enough information to display a confirmation screen and
/// poll for status changes.
/// </summary>
/// <param name="OrderId">Persisted identifier of the new order.</param>
/// <param name="OrderNumber">Human-readable order reference (e.g. "ORD-20240501-0042").</param>
/// <param name="TotalAmount">Final total including tax and shipping.</param>
/// <param name="Status">Initial status string — always "Confirmed" for API-created orders.</param>
public sealed record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    string Status);
