namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// Configuration for <see cref="RetryBehavior{TRequest,TResponse}"/>.
/// Bind from appsettings under the <c>"RetryBehavior"</c> section:
/// <code>
/// "RetryBehavior": {
///   "MaxRetryAttempts":                   3,
///   "InitialDelayMs":                   200,
///   "UseJitter":                         true,
///   "CircuitBreakerMinimumThroughput":    5,
///   "CircuitBreakerFailureRatio":       0.5,
///   "CircuitBreakerSamplingDurationSeconds": 10,
///   "CircuitBreakerBreakDurationSeconds": 30,
///   "BulkheadMaxConcurrency":           100,
///   "BulkheadQueueLimit":                 0
/// }
/// </code>
/// All fields are optional — defaults are production-safe values.
/// </summary>
public sealed class RetryBehaviorOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "RetryBehavior";

    // ── Retry ────────────────────────────────────────────────────────────────────

    /// <summary>Maximum number of retry attempts after the initial failure. Default: 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in milliseconds for the first retry (exponential back-off). Default: 200 ms.</summary>
    public double InitialDelayMs { get; set; } = 200;

    /// <summary>When <see langword="true"/>, adds random jitter to each delay to avoid thundering herd. Default: true.</summary>
    public bool UseJitter { get; set; } = true;

    // ── Circuit Breaker ──────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum number of calls in the sampling window before the circuit breaker
    /// can evaluate the failure ratio. Prevents tripping on cold-start noise.
    /// Default: 5.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Proportion of failures (0.0–1.0) within the sampling window that triggers
    /// the circuit to open. 0.5 means 50% failure rate. Default: 0.5.
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Rolling window in seconds over which failures are measured. Default: 10 s.
    /// </summary>
    public double CircuitBreakerSamplingDurationSeconds { get; set; } = 10;

    /// <summary>
    /// Time in seconds the circuit stays fully open before moving to half-open
    /// and probing the downstream with one request. Default: 30 s.
    /// </summary>
    public double CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    // ── Bulkhead (Concurrency Limiter) ───────────────────────────────────────────

    /// <summary>
    /// Maximum concurrent command executions allowed through this pipeline.
    /// Requests above this limit are rejected immediately (queue = 0) or queued
    /// (queue &gt; 0). Default: 100.
    /// </summary>
    public int BulkheadMaxConcurrency { get; set; } = 100;

    /// <summary>
    /// Maximum number of calls queued behind the concurrency limit.
    /// 0 = reject excess calls immediately (recommended for write commands).
    /// Default: 0.
    /// </summary>
    public int BulkheadQueueLimit { get; set; } = 0;
}
