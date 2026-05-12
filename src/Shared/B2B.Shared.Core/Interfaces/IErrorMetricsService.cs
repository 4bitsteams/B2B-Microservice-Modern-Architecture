namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Records error-level metrics for APM and Grafana dashboards.
///
/// Implementations emit counters to the OpenTelemetry SDK so the
/// OTLP collector can forward them to Prometheus → Grafana.
///
/// ISP: this interface is intentionally narrow — callers (behaviors,
/// middleware) only depend on what they actually need, not a fat
/// "monitoring" facade.
/// </summary>
public interface IErrorMetricsService
{
    /// <summary>
    /// Increments the business-error counter.
    /// Called by <c>ErrorMetricsBehavior</c> when a handler returns
    /// <c>Result.IsFailure</c> after all retry attempts are exhausted.
    /// </summary>
    /// <param name="errorType">The <see cref="B2B.Shared.Core.Common.ErrorType"/> name (e.g. "NotFound").</param>
    /// <param name="requestName">The MediatR request type name (e.g. "CreateOrderCommand").</param>
    void RecordBusinessError(string errorType, string requestName);

    /// <summary>
    /// Increments the unhandled-exception counter.
    /// Called by <c>GlobalExceptionMiddleware</c> for any exception that
    /// escapes the MediatR pipeline without being mapped to a Result.
    /// </summary>
    /// <param name="exceptionType">The CLR exception type name (e.g. "InvalidOperationException").</param>
    /// <param name="requestPath">The HTTP request path (e.g. "/api/orders").</param>
    void RecordUnhandledException(string exceptionType, string requestPath);
}
