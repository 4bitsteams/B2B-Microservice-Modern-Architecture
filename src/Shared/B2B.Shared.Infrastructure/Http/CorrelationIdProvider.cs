using Microsoft.AspNetCore.Http;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Http;

/// <summary>
/// Resolves the active CorrelationId from two sources (priority order):
///
///   1. <c>HttpContext.Items["CorrelationId"]</c> — set by <see cref="CorrelationIdMiddleware"/>
///      for every inbound HTTP request.  Always preferred while a request is in flight.
///
///   2. <see cref="AsyncLocal{T}"/> — set by <see cref="SetForCurrentThread"/> for
///      MassTransit consumers and background workers that run outside an HttpContext.
///      The consumer filter in <see cref="CorrelationIdConsumerFilter{T}"/> calls this
///      after extracting the header from the incoming message.
///
/// Registered as <b>scoped</b> so it gets the correct <see cref="IHttpContextAccessor"/>
/// instance per request.
/// </summary>
public sealed class CorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    : ICorrelationIdProvider
{
    // Shared across all instances — AsyncLocal flows with the async execution context.
    internal static readonly AsyncLocal<string?> Current = new();
    internal const string ItemsKey = "CorrelationId";

    public string CorrelationId
    {
        get
        {
            var fromContext = httpContextAccessor.HttpContext?.Items[ItemsKey] as string;
            return !string.IsNullOrEmpty(fromContext)
                ? fromContext
                : Current.Value ?? string.Empty;
        }
    }

    /// <summary>
    /// Sets the CorrelationId for the current async execution context.
    /// Call this from MassTransit consumer filters or background hosted services
    /// so that <see cref="ICorrelationIdProvider"/> returns the right value
    /// even when there is no <see cref="HttpContext"/>.
    /// </summary>
    public static void SetForCurrentThread(string correlationId) =>
        Current.Value = correlationId;
}
