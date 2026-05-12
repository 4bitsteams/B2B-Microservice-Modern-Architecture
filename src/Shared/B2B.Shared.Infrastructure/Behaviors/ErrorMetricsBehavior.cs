using MediatR;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that records an error metric for every
/// <see cref="Result"/>-based handler response that represents a failure.
///
/// DESIGN (SOLID)
/// ──────────────
/// S — Single Responsibility: records exactly one metric per failed result.
///     Logging is handled by <see cref="LoggingBehavior{TRequest,TResponse}"/>.
///     Audit trail is handled by <see cref="AuditBehavior{TRequest,TResponse}"/>.
/// O — Open/Closed: new error types (added to <see cref="ErrorType"/>) are
///     automatically captured — no changes here required.
/// D — Dependency Inversion: depends on IErrorMetricsService (Core abstraction),
///     not ErrorMetricsService (Infrastructure).
///
/// PIPELINE POSITION (outermost → innermost)
/// ──────────────────────────────────────────
///   LoggingBehavior        ← outermost
///   ErrorMetricsBehavior   ← sees the FINAL result after all retries exhausted
///   RetryBehavior          ← handles transient faults; may produce a ServiceUnavailable Result
///   IdempotencyBehavior
///   PerformanceBehavior
///   AuthorizationBehavior
///   ValidationBehavior
///   AuditBehavior
///   DomainEventBehavior    ← innermost
///
/// Being placed OUTSIDE RetryBehavior means this behavior counts one metric
/// increment per user request regardless of how many retry attempts occurred
/// internally.
///
/// SCOPE
/// ─────
/// Only <see cref="Result"/>-typed responses are inspected. Handlers that return
/// plain types (rare) are passed through unchanged — no metric is recorded.
/// Unhandled exceptions are NOT caught here; they propagate to LoggingBehavior and
/// ultimately to <c>GlobalExceptionMiddleware</c>, which records a separate counter.
/// </summary>
public sealed class ErrorMetricsBehavior<TRequest, TResponse>(
    IErrorMetricsService errorMetrics)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        // Pattern-match against the base Result class.
        // Works for both Result and Result<TValue> because Result<TValue> inherits Result.
        if (response is Result { IsFailure: true } result)
        {
            errorMetrics.RecordBusinessError(
                result.Error.Type.ToString(),
                typeof(TRequest).Name);
        }

        return response;
    }
}
