using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Domain;

namespace B2B.Shared.Infrastructure.Behaviors;

public sealed class DomainEventBehavior<TRequest, TResponse>(
    IPublisher publisher,
    DbContext dbContext,
    ILogger<DomainEventBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();

        // Do not publish domain events when the command returned a business failure —
        // SaveChangesAsync was never called, so no DB state was committed.
        // Publishing events in this case would produce ghost events with no backing record.
        if (response is Result { IsFailure: true })
        {
            ClearTrackedEvents();
            return response;
        }

        var aggregates = dbContext.ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        foreach (var domainEvent in events)
        {
            logger.LogInformation("Publishing domain event {EventType}", domainEvent.GetType().Name);
            await publisher.Publish(domainEvent, ct);
        }

        return response;
    }

    // Clears domain-event lists from all tracked aggregates without publishing.
    // Called on failure to prevent memory accumulation across request retries.
    private void ClearTrackedEvents()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<AggregateRoot<Guid>>())
            entry.Entity.ClearDomainEvents();
    }
}
