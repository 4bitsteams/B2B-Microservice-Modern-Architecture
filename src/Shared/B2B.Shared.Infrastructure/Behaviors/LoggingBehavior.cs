using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that emits structured request/response logs.
///
/// Every log line includes <c>CorrelationId</c> so a single value ties together
/// the HTTP request log (from Serilog), the MediatR command/query logs (here),
/// the EF Core query logs, and any outgoing Kafka messages — all in one
/// Seq/Jaeger search.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICorrelationIdProvider correlationIdProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName  = typeof(TRequest).Name;
        var correlationId = correlationIdProvider.CorrelationId;
        var sw = Stopwatch.StartNew();

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

                logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMs}ms [{CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, correlationId);

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex,
                    "Error handling {RequestName} after {ElapsedMs}ms [{CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, correlationId);
                throw;
            }
        }
    }
}
