using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that emits structured request/response logs.
///
/// Every log line includes <c>CorrelationId</c> so a single value ties together
/// the HTTP request log (from Serilog), the MediatR command/query logs (here),
/// the EF Core query logs, and any outgoing Kafka messages — all in one
/// Seq/Jaeger search.
///
/// APM / ERROR LOG HANDLING
/// ────────────────────────
/// When a handler returns <see cref="Result.IsFailure"/>, a structured
/// <c>LogWarning</c> is emitted with the full error code, type, and description.
/// This feeds:
///   • Seq   — query by ErrorCode / ErrorType for alert rules
///   • Jaeger — the active span is marked with an "error" attribute
///   • Grafana — log panels can filter on {Level="Warning", RequestName="..."}
///
/// Unhandled exceptions are still re-thrown so <c>GlobalExceptionMiddleware</c>
/// can return a structured ProblemDetails response and increment the separate
/// <c>unhandled_exceptions</c> metric.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICorrelationIdProvider correlationIdProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName   = typeof(TRequest).Name;
        var correlationId = correlationIdProvider.CorrelationId;
        var sw            = Stopwatch.StartNew();

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["RequestName"]   = requestName
               }))
        {
            logger.LogInformation(
                "Handling {RequestName} [{CorrelationId}]", requestName, correlationId);

            try
            {
                var response = await next();
                sw.Stop();

                // ── Result failure — log a structured warning for APM dashboards ──
                if (response is Result { IsFailure: true } result)
                {
                    // Mark the active OTel span as an error so Jaeger highlights it.
                    Activity.Current?.SetStatus(ActivityStatusCode.Error, result.Error.Description);
                    Activity.Current?.SetTag("error.type",        result.Error.Type.ToString());
                    Activity.Current?.SetTag("error.code",        result.Error.Code);
                    Activity.Current?.SetTag("error.description", result.Error.Description);

                    logger.LogWarning(
                        "Business failure | {RequestName} [{CorrelationId}] | " +
                        "ErrorType: {ErrorType} | ErrorCode: {ErrorCode} | {ErrorDescription} | ElapsedMs: {ElapsedMs}",
                        requestName, correlationId,
                        result.Error.Type, result.Error.Code, result.Error.Description,
                        sw.ElapsedMilliseconds);
                }
                else
                {
                    logger.LogInformation(
                        "Handled {RequestName} in {ElapsedMs}ms [{CorrelationId}]",
                        requestName, sw.ElapsedMilliseconds, correlationId);
                }

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();

                // Mark the OTel span as failed for Jaeger trace visibility.
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Activity.Current?.SetTag("exception.type",    ex.GetType().Name);
                Activity.Current?.SetTag("exception.message", ex.Message);

                logger.LogError(ex,
                    "Unhandled exception | {RequestName} [{CorrelationId}] | " +
                    "ExceptionType: {ExceptionType} | ElapsedMs: {ElapsedMs}",
                    requestName, correlationId, ex.GetType().Name, sw.ElapsedMilliseconds);

                throw;
            }
        }
    }
}
