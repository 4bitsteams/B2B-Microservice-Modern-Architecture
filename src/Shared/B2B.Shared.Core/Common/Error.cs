namespace B2B.Shared.Core.Common;

/// <summary>
/// Represents a domain error returned from application layer operations.
/// Errors are typed via <see cref="ErrorType"/> so API controllers can map them
/// to the correct HTTP status code without inspecting error codes.
///
/// Create errors via the static factory methods rather than the primary
/// constructor to ensure the correct <see cref="ErrorType"/> is always set:
/// <code>
/// return Error.NotFound("Order.NotFound", $"Order {id} was not found.");
/// return Error.Validation("Order.EmptyItems", "Order must have at least one item.");
/// return Error.Conflict("Product.SkuExists", $"SKU '{sku}' already exists.");
/// </code>
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    /// <summary>Sentinel value indicating the absence of an error (success path).</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error. Maps to HTTP 404.</summary>
    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error. Maps to HTTP 400.</summary>
    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error. Maps to HTTP 409.</summary>
    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    /// <summary>Creates an <see cref="ErrorType.Unauthorized"/> error. Maps to HTTP 401.</summary>
    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    /// <summary>Creates a <see cref="ErrorType.Forbidden"/> error. Maps to HTTP 403.</summary>
    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    /// <summary>Creates a generic <see cref="ErrorType.Failure"/> error. Maps to HTTP 500.</summary>
    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);
}

/// <summary>
/// Classifies the kind of error so callers can map it to the appropriate HTTP status code
/// or branch their error-handling logic without string-comparing error codes.
/// </summary>
public enum ErrorType
{
    /// <summary>No error — used by <see cref="Error.None"/> on the success path.</summary>
    None,

    /// <summary>An unexpected internal error. Maps to HTTP 500.</summary>
    Failure,

    /// <summary>One or more input fields failed validation. Maps to HTTP 400.</summary>
    Validation,

    /// <summary>The requested resource does not exist. Maps to HTTP 404.</summary>
    NotFound,

    /// <summary>The operation would create a duplicate or violate a uniqueness constraint. Maps to HTTP 409.</summary>
    Conflict,

    /// <summary>The caller is not authenticated. Maps to HTTP 401.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but lacks permission. Maps to HTTP 403.</summary>
    Forbidden
}
