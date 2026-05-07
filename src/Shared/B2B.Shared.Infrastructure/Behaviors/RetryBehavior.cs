using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that applies a two-layer Polly 8 resilience pipeline
/// plus a bulkhead guard to all <see cref="B2B.Shared.Core.CQRS.ICommand"/> /
/// <see cref="B2B.Shared.Core.CQRS.ICommand{TResponse}"/> requests.
///
/// Execution order (outermost → innermost):
///
///   1. Bulkhead — per-handler <see cref="SemaphoreSlim"/> caps concurrent writes.
///      Saturated calls are rejected immediately so callers get a fast 503 rather
///      than queuing indefinitely behind a slow downstream.
///
///   2. Circuit Breaker — tracks rolling failure ratio. When it exceeds the threshold
///      the circuit opens and all commands fail fast for <c>BreakDuration</c> seconds.
///
///   3. Retry — exponential back-off + jitter for transient faults (I/O, timeouts).
///      Runs only when the circuit is closed.
///
/// Configuration: <see cref="RetryBehaviorOptions"/> bound from <c>"RetryBehavior"</c>
/// in appsettings. All fields are optional with production-safe defaults.
/// </summary>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Computed once per closed generic type via ResultHelper's JIT-static cache.
    private static readonly bool IsCommand = ResultHelper.IsCommandRequest<TRequest>();
    private static readonly bool IsResult  = ResultHelper.IsResultResponse<TResponse>();

    // Shared predicate for both circuit breaker and retry — single definition, no duplication.
    private static readonly PredicateBuilder<TResponse> TransientFaultPredicate =
        new PredicateBuilder<TResponse>()
            .Handle<IOException>()
            .Handle<TimeoutException>()
            .Handle<InvalidOperationException>(ex =>
                ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timeout",    StringComparison.OrdinalIgnoreCase));

    private readonly ResiliencePipeline<TResponse> _pipeline;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly SemaphoreSlim _bulkhead;
    private readonly int _bulkheadMaxConcurrency;

    public RetryBehavior(
        ILogger<RetryBehavior<TRequest, TResponse>> logger,
        IOptions<RetryBehaviorOptions> options,
        CommandBulkheadProvider bulkheadProvider)
    {
        _logger = logger;
        var opts = options.Value;
        _bulkhead = bulkheadProvider.GetOrCreate<TRequest>(opts.BulkheadMaxConcurrency);
        _bulkheadMaxConcurrency = opts.BulkheadMaxConcurrency;
        var maxAttempts = opts.MaxRetryAttempts;

        _pipeline = new ResiliencePipelineBuilder<TResponse>()

            // ── Circuit Breaker ──────────────────────────────────────────────────
            // Opens when FailureRatio% of the last MinimumThroughput calls fail within
            // SamplingDuration. Stays open for BreakDuration before half-open probe.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResponse>
            {
                FailureRatio = opts.CircuitBreakerFailureRatio,
                MinimumThroughput = opts.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(opts.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakerBreakDurationSeconds),
                ShouldHandle = TransientFaultPredicate,
                OnOpened = args =>
                {
                    logger.LogError(args.Outcome.Exception,
                        "Circuit breaker OPEN for {Request} — failing fast for {Duration}s",
                        typeof(TRequest).Name, opts.CircuitBreakerBreakDurationSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation(
                        "Circuit breaker CLOSED for {Request} — normal operation resumed",
                        typeof(TRequest).Name);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation(
                        "Circuit breaker HALF-OPEN for {Request} — probing downstream",
                        typeof(TRequest).Name);
                    return ValueTask.CompletedTask;
                }
            })

            // ── Retry ────────────────────────────────────────────────────────────
            // Retries transient faults up to MaxRetryAttempts with exponential
            // back-off + jitter. Runs only when the circuit is closed.
            .AddRetry(new RetryStrategyOptions<TResponse>
            {
                MaxRetryAttempts = opts.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = opts.UseJitter,
                Delay = TimeSpan.FromMilliseconds(opts.InitialDelayMs),
                ShouldHandle = TransientFaultPredicate,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient failure in {Request} — attempt {Attempt} of {Max}. Retrying…",
                        typeof(TRequest).Name, args.AttemptNumber + 1, maxAttempts);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand) return await next();

        // ── Bulkhead guard ───────────────────────────────────────────────────────
        // WaitAsync(0) = non-blocking check — reject immediately if saturated.
        if (!await _bulkhead.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning(
                "Bulkhead saturated for {Request} — max concurrency {Max} reached",
                typeof(TRequest).Name, _bulkheadMaxConcurrency);

            if (IsResult)
                return (TResponse)ResultHelper.Failure(typeof(TResponse),
                    Error.ServiceUnavailable("Bulkhead.Full",
                        "Server is currently handling too many requests. Please retry shortly."));
            throw new InvalidOperationException(
                $"Bulkhead saturated — max {_bulkheadMaxConcurrency} concurrent {typeof(TRequest).Name} commands.");
        }

        try
        {
            return await _pipeline.ExecuteAsync(async _ => await next(), cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit open — {Request} rejected (service unavailable)",
                typeof(TRequest).Name);

            if (IsResult)
                return (TResponse)ResultHelper.Failure(typeof(TResponse),
                    Error.ServiceUnavailable("CircuitBreaker.Open",
                        $"Service temporarily unavailable. '{typeof(TRequest).Name}' rejected while circuit is open."));
            throw;
        }
        finally
        {
            _bulkhead.Release();
        }
    }
}
