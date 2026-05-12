namespace B2B.Web.Models.Orders;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal TotalAmount,
    decimal SubTotal,
    decimal TaxAmount,
    string? Notes,
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    List<OrderItemDto> Items,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal SubTotal);

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

public sealed record CreateOrderRequest(
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    List<OrderItemRequest> Items,
    string? Notes);

public sealed record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);

public sealed record AddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

public sealed record CancelOrderRequest(string Reason);

public sealed record ShipOrderRequest(string TrackingNumber, string Carrier);
