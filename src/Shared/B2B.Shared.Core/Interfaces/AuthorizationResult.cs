namespace B2B.Shared.Core.Interfaces;

/// <summary>Outcome of a resource-based authorization check performed by <see cref="IAuthorizer{TRequest}"/>.</summary>
public sealed record AuthorizationResult(bool IsAuthorized, string? FailureReason = null)
{
    /// <summary>Returns a successful authorization result.</summary>
    public static AuthorizationResult Success() => new(true);

    /// <summary>Returns a failed authorization result with the supplied <paramref name="reason"/>.</summary>
    public static AuthorizationResult Fail(string reason) => new(false, reason);
}
