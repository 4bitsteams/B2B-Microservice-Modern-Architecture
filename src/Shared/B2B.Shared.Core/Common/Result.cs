namespace B2B.Shared.Core.Common;

/// <summary>
/// Discriminated union that represents the outcome of an operation — either
/// success or a typed <see cref="Error"/> — without throwing exceptions for
/// expected business failures.
///
/// <para>
/// Handlers return <see cref="Result{TValue}"/> so controllers can inspect
/// <see cref="IsSuccess"/> and branch to the appropriate HTTP response via
/// <c>result.Error.Type</c>.
/// </para>
///
/// Usage:
/// <code>
/// // Return success
/// return new CreateOrderResponse(order.Id, order.OrderNumber);
///
/// // Return failure
/// return Error.NotFound("Order.NotFound", $"Order {id} was not found.");
/// </code>
/// </summary>
public class Result
{
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error"><see cref="Error.None"/> on success; a typed error on failure.</param>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary><see langword="true"/> when the operation completed without error.</summary>
    public bool IsSuccess { get; }

    /// <summary><see langword="true"/> when the operation produced an error.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error associated with this result.
    /// Returns <see cref="Error.None"/> on the success path.
    /// </summary>
    public Error Error { get; }

    /// <summary>Creates a successful result with no value.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a failed result carrying the given <paramref name="error"/>.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>Creates a failed result of type <typeparamref name="TValue"/> carrying the given <paramref name="error"/>.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>Implicitly converts an <see cref="Error"/> to a failed <see cref="Result"/>.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Extends <see cref="Result"/> with a typed <typeparamref name="TValue"/> payload
/// available on the success path via <see cref="Value"/>.
/// </summary>
/// <typeparam name="TValue">The type of the value carried on success.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <param name="value">The success value; <see langword="default"/> on failure.</param>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The error, or <see cref="Error.None"/> on success.</param>
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>
    /// The success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    /// <summary>Implicitly converts a value of <typeparamref name="TValue"/> to a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly converts an <see cref="Error"/> to a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
