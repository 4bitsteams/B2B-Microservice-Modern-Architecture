using System.Collections.Concurrent;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// Factory for creating <see cref="Result"/> and <see cref="Result{TValue}"/> instances
/// without repeating reflection logic at every call site.
///
/// DESIGN
/// ──────
/// Reflection (<c>GetMethod</c> + <c>MakeGenericMethod</c>) runs <b>once per closed generic
/// type</b>: the compiled delegate is stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by <see cref="Type"/>, so subsequent calls are allocation-free dictionary lookups.
///
/// USAGE
/// ─────
/// <code>
/// // In a pipeline behavior:
/// return ResultHelper.Failure&lt;TResponse&gt;(Error.Forbidden("Auth.Failed", reason));
/// return ResultHelper.Success&lt;TResponse&gt;(deserializedValue);
/// </code>
///
/// This eliminates the duplicated reflection blocks that previously existed in
/// <see cref="ValidationBehavior{TRequest,TResponse}"/>,
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/>, and
/// <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.
/// </summary>
internal static class ResultHelper
{
    // ── Failure factories ─────────────────────────────────────────────────────
    // Each entry: (Error error) → boxed Result / Result<T>

    private static readonly ConcurrentDictionary<Type, Func<Error, object>> FailureFactories = new();

    /// <summary>
    /// Returns a <typeparamref name="TResult"/> in failure state wrapping <paramref name="error"/>.
    /// Works for both non-generic <see cref="Result"/> and <see cref="Result{TValue}"/>.
    /// </summary>
    public static TResult Failure<TResult>(Error error) where TResult : Result
        => (TResult)FailureFactories.GetOrAdd(typeof(TResult), BuildFailureFactory)(error);

    /// <summary>
    /// Non-generic overload for call sites where the concrete Result type is known
    /// only at runtime (e.g. <see cref="AuthorizationBehavior{TRequest,TResponse}"/>
    /// which is not constrained to <c>Result</c>).
    /// </summary>
    public static object Failure(Type resultType, Error error)
        => FailureFactories.GetOrAdd(resultType, BuildFailureFactory)(error);

    private static Func<Error, object> BuildFailureFactory(Type resultType)
    {
        if (resultType == typeof(Result))
            return error => Result.Failure(error);

        var inner = resultType.GetGenericArguments()[0];
        var method = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(inner);

        return error => method.Invoke(null, [error])!;
    }

    // ── Success factories ─────────────────────────────────────────────────────
    // Each entry: (object? value) → boxed Result / Result<T>

    private static readonly ConcurrentDictionary<Type, Func<object?, object>> SuccessFactories = new();

    /// <summary>
    /// Returns a <typeparamref name="TResult"/> in success state.
    /// Pass <paramref name="value"/> for <c>Result&lt;T&gt;</c>; omit for non-generic <c>Result</c>.
    /// </summary>
    public static TResult Success<TResult>(object? value = null) where TResult : Result
        => (TResult)SuccessFactories.GetOrAdd(typeof(TResult), BuildSuccessFactory)(value);

    private static Func<object?, object> BuildSuccessFactory(Type resultType)
    {
        if (resultType == typeof(Result))
            return _ => Result.Success();

        var inner = resultType.GetGenericArguments()[0];
        var method = typeof(Result)
            .GetMethods()
            .First(m => m.Name == nameof(Result.Success) && m.IsGenericMethod)
            .MakeGenericMethod(inner);

        return value => method.Invoke(null, [value])!;
    }
}
