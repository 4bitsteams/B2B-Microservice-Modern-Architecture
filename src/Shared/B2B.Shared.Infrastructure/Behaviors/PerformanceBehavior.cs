using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that warns on slow commands and queries.
///
/// Thresholds (tuned for B2B workloads — B2B reads are typically heavier
/// than consumer apps due to multi-tenant filtering and complex joins):
///   Commands : warn after 500 ms  (write path — DB + outbox + events)
///   Queries  : warn after 200 ms  (read path — should be served from replica or cache)
///
/// Slow-query warnings include the current user ID and tenant to aid debugging
/// in multi-tenant environments where one tenant's data volume may cause
/// disproportionate latency.
///
/// Does NOT block the request — purely observational.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Computed once per closed generic type — avoids per-request reflection.
    // Checks the IQuery<> marker interface rather than relying on a naming convention.
    private static readonly bool IsQuery = typeof(TRequest).GetInterfaces()
        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    // Warn when commands take longer than this.
    private const int CommandThresholdMs = 500;

    // Warn when queries take longer than this.
    private const int QueryThresholdMs = 200;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        var elapsed = sw.ElapsedMilliseconds;
        var requestName = typeof(TRequest).Name;
        var threshold = IsQuery ? QueryThresholdMs : CommandThresholdMs;

        if (elapsed > threshold)
        {
            logger.LogWarning(
                "Slow {RequestType} detected: {Request} took {Elapsed}ms " +
                "(threshold: {Threshold}ms) | User: {UserId} | Tenant: {TenantId}",
                IsQuery ? "Query" : "Command",
                requestName,
                elapsed,
                threshold,
                currentUser.IsAuthenticated ? currentUser.UserId : (Guid?)null,
                currentUser.IsAuthenticated ? currentUser.TenantId : (Guid?)null);
        }

        return response;
    }
}
