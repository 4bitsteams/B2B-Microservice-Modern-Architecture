using B2B.Shared.Core.Domain;

namespace B2B.Discount.Domain.Events;

public sealed record DiscountCreatedEvent(Guid DiscountId, string Name, string Type, decimal Value) : DomainEvent;
public sealed record DiscountDeactivatedEvent(Guid DiscountId, string Name) : DomainEvent;
public sealed record CouponCreatedEvent(Guid CouponId, string Code, string Name) : DomainEvent;
public sealed record CouponUsedEvent(Guid CouponId, string Code) : DomainEvent;
