using System.Diagnostics.Metrics;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Observability;

/// <summary>
/// OpenTelemetry-backed implementation of <see cref="IErrorMetricsService"/>.
///
/// Uses <see cref="System.Diagnostics.Metrics"/> — the standard .NET metrics API —
/// so the counters are automatically picked up by the OTel SDK when the meter
/// "B2B.Platform" is registered via <c>AddMeter("B2B.Platform")</c>.
///
/// DESIGN (SOLID)
/// ──────────────
/// S — Single Responsibility: records error counters only; no logging, no alerting.
/// D — Dependency Inversion: callers depend on IErrorMetricsService (Core), not this class.
/// O — Open/Closed: new metric instruments can be added without touching callers.
///
/// METRIC NAMING CONVENTION
/// ────────────────────────
/// Instrument names follow the OpenTelemetry semantic conventions (lowercase, dots as
/// separators). The OTel → Prometheus exporter maps dots to underscores and the
/// OTel Collector's namespace "b2b" prefixes them, yielding:
///   business_errors       → b2b_business_errors_total
///   unhandled_exceptions  → b2b_unhandled_exceptions_total
/// </summary>
public sealed class ErrorMetricsService : IErrorMetricsService
{
    /// <summary>OTel meter name — must match the name passed to AddMeter() in DI setup.</summary>
    public const string MeterName = "B2B.Platform";

    private readonly Counter<long> _businessErrorCounter;
    private readonly Counter<long> _unhandledExceptionCounter;

    public ErrorMetricsService(IMeterFactory meterFactory)
    {
        // IMeterFactory is the DI-friendly factory introduced in .NET 8.
        // Using it (instead of new Meter()) respects the lifetime managed by the OTel SDK.
        var meter = meterFactory.Create(MeterName);

        _businessErrorCounter = meter.CreateCounter<long>(
            name: "business_errors",
            unit: "{error}",
            description: "Total business errors returned by MediatR handlers, tagged by error type and request name.");

        _unhandledExceptionCounter = meter.CreateCounter<long>(
            name: "unhandled_exceptions",
            unit: "{exception}",
            description: "Total unhandled exceptions that escaped the MediatR pipeline, tagged by exception type and HTTP path.");
    }

    /// <inheritdoc />
    public void RecordBusinessError(string errorType, string requestName)
    {
        _businessErrorCounter.Add(1,
            new KeyValuePair<string, object?>("error_type",   errorType),
            new KeyValuePair<string, object?>("request_name", requestName));
    }

    /// <inheritdoc />
    public void RecordUnhandledException(string exceptionType, string requestPath)
    {
        _unhandledExceptionCounter.Add(1,
            new KeyValuePair<string, object?>("exception_type", exceptionType),
            new KeyValuePair<string, object?>("request_path",   requestPath));
    }
}
