using System.Collections.Concurrent;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

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
/// TYPE CLASSIFICATION
/// ───────────────────
/// <c>RequestTypeCache&lt;T&gt;</c> and <c>ResultTypeCache&lt;T&gt;</c> use the JIT's
/// per-generic-instantiation static-field guarantee as a zero-allocation alternative to
/// <c>ConcurrentDictionary</c> for boolean type checks shared across pipeline behaviors.
/// Each unique <c>T</c> is evaluated exactly once with no locking overhead.
///
/// USAGE
/// ─────
/// <code>
/// // Result factories — in a pipeline behavior:
/// return ResultHelper.Failure&lt;TResponse&gt;(Error.Forbidden("Auth.Failed", reason));
/// return ResultHelper.Success&lt;TResponse&gt;(deserializedValue);
///
/// // Type classification — eliminates per-behavior duplication:
/// if (!ResultHelper.IsCommandRequest&lt;TRequest&gt;()) return await next();
/// if (ResultHelper.IsResultResponse&lt;TResponse&gt;()) return (TResponse)ResultHelper.Failure(...);
/// </code>
///
/// This eliminates the duplicated reflection blocks that previously existed in
/// <see cref="ValidationBehavior{TRequest,TResponse}"/>,
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/>,
/// <see cref="IdempotencyBehavior{TRequest,TResponse}"/>,
/// <see cref="AuditBehavior{TRequest,TResponse}"/>, and
/// <see cref="RetryBehavior{TRequest,TResponse}"/>.
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
        // Single() is deliberate: Result must have exactly one generic Success overload.
        // First() would silently pick the wrong overload if the API ever gains a second.
        var method = typeof(Result)
            .GetMethods()
            .Single(m => m.Name == nameof(Result.Success) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(inner);

        return value => method.Invoke(null, [value])!;
    }

    // ── Type classification ───────────────────────────────────────────────────
    // Uses the JIT's static-field-per-generic-instantiation guarantee so each
    // unique T is computed exactly once — no ConcurrentDictionary, no locking.

    /// <summary>
    /// <see langword="true"/> when <typeparamref name="TRequest"/> implements
    /// <see cref="ICommand"/> or <see cref="ICommand{TResponse}"/>.
    /// </summary>
    internal static bool IsCommandRequest<TRequest>() => RequestTypeCache<TRequest>.IsCommand;

    /// <summary>
    /// <see langword="true"/> when <typeparamref name="TRequest"/> implements
    /// <see cref="IQuery{TResponse}"/>.
    /// </summary>
    internal static bool IsQueryRequest<TRequest>() => RequestTypeCache<TRequest>.IsQuery;

    /// <summary>
    /// <see langword="true"/> when <typeparamref name="TResponse"/> is or derives from
    /// <see cref="Result"/> — i.e. it can carry a failure value back to the caller.
    /// </summary>
    internal static bool IsResultResponse<TResponse>() => ResultTypeCache<TResponse>.IsResult;

    private static class RequestTypeCache<T>
    {
        internal static readonly bool IsCommand = typeof(T).GetInterfaces()
            .Any(i => i == typeof(ICommand) ||
                      (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

        internal static readonly bool IsQuery = typeof(T).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
    }

    private static class ResultTypeCache<T>
    {
        internal static readonly bool IsResult = typeof(T).IsAssignableTo(typeof(Result));
    }
}
