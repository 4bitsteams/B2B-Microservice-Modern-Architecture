using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace B2B.Shared.Infrastructure.Http;

/// <summary>
/// ASP.NET Core middleware that establishes a CorrelationId for every request.
///
/// Behavior:
///   • Reads <c>X-Correlation-ID</c> from the incoming request headers.
///     The API Gateway stamps this header on every proxied request, so downstream
///     services always receive a pre-existing ID that ties all their logs to the
///     same originating client request.
///   • If the header is absent (direct calls, tests, health checks) a new
///     lowercase compact GUID is generated.
///   • The ID is stored in both:
///       - <see cref="HttpContext.Items"/> — synchronously available within the
///         current request via <see cref="CorrelationIdProvider"/>.
///       - <see cref="CorrelationIdProvider.Current"/> (AsyncLocal) — flows
///         automatically into async continuations and spawned Tasks.
///   • The response always echoes the ID back so callers can log it.
///   • The ID is pushed into the Serilog/ILogger scope so every log line
///     emitted during the request automatically includes <c>CorrelationId</c>.
///
/// Register before authentication so the ID is available in all subsequent middleware:
/// <code>app.UseCorrelationId();</code>
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Expose to ICorrelationIdProvider via HttpContext (HTTP path)
        context.Items[CorrelationIdProvider.ItemsKey] = correlationId;

        // Expose to ICorrelationIdProvider via AsyncLocal (async continuations)
        CorrelationIdProvider.SetForCurrentThread(correlationId);

        // Echo back so the caller can correlate its own logs
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await next(context);
        }
    }
}
