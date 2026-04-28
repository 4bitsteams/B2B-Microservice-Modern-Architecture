namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Resource-based authorization for a specific request type.
///
/// Implement one authorizer per command/query that requires authorization
/// beyond role checks — e.g. "can this user cancel THIS specific order?"
/// Register implementations in the DI container; the <c>AuthorizationBehavior</c>
/// pipeline step resolves all matching authorizers and runs them before the handler.
///
/// No authorizer registered for a request type means the request is considered
/// pre-authorized (controller-level [Authorize] still applies).
/// </summary>
public interface IAuthorizer<in TRequest>
{
    Task<AuthorizationResult> AuthorizeAsync(TRequest request, CancellationToken ct = default);
}
