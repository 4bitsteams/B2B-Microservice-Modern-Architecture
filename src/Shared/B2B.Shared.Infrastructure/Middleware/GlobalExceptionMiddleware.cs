using System.Diagnostics;
using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Middleware;

/// <summary>
/// Catches any <see cref="Exception"/> that escapes the MediatR pipeline or other
/// middleware and converts it into a structured <see cref="ProblemDetails"/> response.
///
/// DESIGN (SOLID)
/// ──────────────
/// S — Single Responsibility: maps unhandled exceptions to HTTP 500 responses and
///     emits one metric increment. It does NOT contain business logic.
/// O — Open/Closed: new exception types can be handled by adding a new middleware
///     (or by converting them to Result failures inside handlers). This class stays closed.
/// D — Dependency Inversion: depends on ILogger and IErrorMetricsService abstractions.
///
/// PIPELINE POSITION
/// ─────────────────
/// Register AFTER UseCorrelationId() so the correlation ID is already stamped in
/// HttpContext.Items before this middleware's catch block reads it.
///
///   UseCorrelationId()         ← stamps X-Correlation-ID
///   UseGlobalExceptionHandler() ← catches everything below with the ID available
///   UseResponseCompression()
///   ... controllers / MediatR pipeline ...
///
/// SECURITY
/// ────────
/// Internal exception details (stack trace, inner exceptions) are intentionally
/// suppressed in the response body to prevent information disclosure.
/// The full exception is written to the structured log (Serilog → Seq) and the
/// OTel trace (Jaeger), where it is accessible to operators but not callers.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IErrorMetricsService errorMetrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Prefer the correlation ID stamped by CorrelationIdMiddleware.
        // Fall back to the raw header value, then generate a fresh GUID so the
        // response always carries a traceable ID even if middleware ordering is wrong.
        var correlationId =
            context.Items["CorrelationId"] as string
            ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Include the current OTel trace ID so operators can jump from Seq to Jaeger.
        var traceId = Activity.Current?.TraceId.ToString();

        logger.LogError(ex,
            "Unhandled exception | Method: {Method} | Path: {Path} | " +
            "ExceptionType: {ExceptionType} | CorrelationId: {CorrelationId} | TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            ex.GetType().Name,
            correlationId,
            traceId);

        // Increment the metric so Grafana can alert on unhandled exception spikes.
        errorMetrics.RecordUnhandledException(ex.GetType().Name, context.Request.Path);

        // Mark the active OTel span as failed so Jaeger shows a red trace.
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

        // Do not overwrite a response that has already been partially streamed.
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title  = "An unexpected error occurred.",
            Detail = "An internal server error occurred. " +
                     "Please contact support and provide the correlation ID.",
        };

        // Expose only safe identifiers — never the stack trace.
        problem.Extensions["correlationId"] = correlationId;

        if (traceId is not null)
            problem.Extensions["traceId"] = traceId;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
