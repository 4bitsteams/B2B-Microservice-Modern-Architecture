using B2B.Shared.Core.Domain;

namespace B2B.Vendor.Domain.Events;

public sealed record VendorRegisteredEvent(Guid VendorId, string CompanyName, string Email) : DomainEvent;
public sealed record VendorApprovedEvent(Guid VendorId, string CompanyName, decimal CommissionRate) : DomainEvent;
public sealed record VendorSuspendedEvent(Guid VendorId, string CompanyName, string Reason) : DomainEvent;
public sealed record VendorDeactivatedEvent(Guid VendorId, string CompanyName) : DomainEvent;
