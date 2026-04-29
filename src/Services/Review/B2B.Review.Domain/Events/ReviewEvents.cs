using B2B.Shared.Core.Domain;

namespace B2B.Review.Domain.Events;

public sealed record ReviewSubmittedEvent(Guid ReviewId, Guid ProductId, Guid CustomerId, int Rating) : DomainEvent;
public sealed record ReviewApprovedEvent(Guid ReviewId, Guid ProductId) : DomainEvent;
