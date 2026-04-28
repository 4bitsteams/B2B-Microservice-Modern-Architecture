using MediatR;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Core.CQRS;

/// <summary>
/// Marker interface for read-only queries that return a typed response.
/// Queries must not mutate state; inject <c>IReadRepository</c> variants
/// (NoTracking, read-replica) rather than write repositories.
/// Implement via <see cref="IQueryHandler{TQuery, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The query result type, wrapped in <see cref="Result{TResponse}"/>.</typeparam>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

/// <summary>
/// MediatR handler contract for read-only queries.
/// Query handlers should only inject <c>IReadRepository</c> implementations and
/// <see cref="B2B.Shared.Core.Interfaces.ICurrentUser"/> for tenant scoping.
/// They pass through the Logging pipeline behavior only — Validation and
/// DomainEvent behaviors are bypassed for queries.
/// </summary>
/// <typeparam name="TQuery">The query type handled.</typeparam>
/// <typeparam name="TResponse">The result type returned on success.</typeparam>
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
