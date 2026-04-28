using B2B.Shared.Core.Domain;

namespace B2B.Identity.Domain.Events;

public sealed record UserRegisteredEvent(Guid UserId, string Email, Guid TenantId) : DomainEvent;
public sealed record UserEmailVerifiedEvent(Guid UserId, string Email) : DomainEvent;
public sealed record UserLoggedInEvent(Guid UserId, string Email, DateTime LoginAt) : DomainEvent;
public sealed record UserLockedEvent(Guid UserId, string Email, DateTime LockedUntil) : DomainEvent;
