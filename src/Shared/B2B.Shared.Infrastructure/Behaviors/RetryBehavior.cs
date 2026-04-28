using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using B2B.Shared.Core.CQRS;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically retries transient failures.
///
/// Targets only commands (<see cref="ICommand"/> / <see cref="ICommand{TResponse}"/>)
/// because queries are idempotent by nature and callers can retry safely on their own.
///
/// Retry strategy (exponential back-off with optional jitter) — configurable via
/// <see cref="RetryBehaviorOptions"/> (bound from appsettings <c>"RetryBehavior"</c> section):
///   Attempt 1 → immediate
///   Attempt 2 → ~InitialDelayMs
///   Attempt 3 → ~InitialDelayMs × 2
///   Attempt 4 → ~InitialDelayMs × 4  (default: max 3 retries = 4 total attempts)
///
/// Transient exceptions targeted:
///   - Npgsql/EF Core transient connection failures
///   - Redis StackExchange transient failures
///   - General I/O and timeout exceptions
/// </summary>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Static per closed generic type — computed once by the JIT, shared across all
    // instances of RetryBehavior<TRequest, TResponse> for the same TRequest type.
    private static readonly bool IsCommand = typeof(TRequest).GetInterfaces()
        .Any(i => i == typeof(ICommand) ||
                  (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    private readonly ResiliencePipeline<TResponse> _pipeline;

    public RetryBehavior(
        ILogger<RetryBehavior<TRequest, TResponse>> logger,
        IOptions<RetryBehaviorOptions> options)
    {
        var opts = options.Value;
        var maxAttempts = opts.MaxRetryAttempts;

        _pipeline = new ResiliencePipelineBuilder<TResponse>()
            .AddRetry(new RetryStrategyOptions<TResponse>
            {
                MaxRetryAttempts = opts.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = opts.UseJitter,
                Delay = TimeSpan.FromMilliseconds(opts.InitialDelayMs),
                ShouldHandle = new PredicateBuilder<TResponse>()
                    .Handle<InvalidOperationException>(ex =>
                        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    .Handle<IOException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient failure in {Request} — attempt {Attempt} of {Max}",
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

        return await _pipeline.ExecuteAsync(async _ => await next(), cancellationToken);
    }
}
