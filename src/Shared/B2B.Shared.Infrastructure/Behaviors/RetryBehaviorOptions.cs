namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// Configuration for <see cref="RetryBehavior{TRequest,TResponse}"/>.
/// Bind from appsettings under the <c>"RetryBehavior"</c> section:
/// <code>
/// "RetryBehavior": {
///   "MaxRetryAttempts": 3,
///   "InitialDelayMs":   200,
///   "UseJitter":        true
/// }
/// </code>
/// All fields are optional — defaults match the original hardcoded values.
/// </summary>
public sealed class RetryBehaviorOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "RetryBehavior";

    /// <summary>Maximum number of retry attempts after the initial failure. Default: 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in milliseconds for the first retry (exponential back-off). Default: 200 ms.</summary>
    public double InitialDelayMs { get; set; } = 200;

    /// <summary>When <see langword="true"/>, adds random jitter to each delay to avoid thundering herd. Default: true.</summary>
    public bool UseJitter { get; set; } = true;
}
