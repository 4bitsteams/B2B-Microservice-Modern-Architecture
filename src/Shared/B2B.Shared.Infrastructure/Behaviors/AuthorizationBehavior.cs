using MediatR;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior for resource-based authorization.
///
/// Receives all <see cref="IAuthorizer{TRequest}"/> implementations registered
/// for the current request type via constructor injection and runs them in
/// parallel before the handler executes. If any authorizer rejects the request,
/// the pipeline short-circuits with <see cref="ErrorType.Forbidden"/>.
///
/// Pipeline position: BEFORE <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// (fast-fail on unauthorized requests before spending time on validation) and
/// BEFORE <see cref="DomainEventBehavior{TRequest,TResponse}"/> (no DB writes
/// happen unless the caller is authorized).
///
/// Usage — register an authorizer for a specific command:
/// <code>
/// public class CancelOrderAuthorizer(ICurrentUser currentUser, IOrderRepository orders)
///     : IAuthorizer&lt;CancelOrderCommand&gt;
/// {
///     public async Task&lt;AuthorizationResult&gt; AuthorizeAsync(CancelOrderCommand request, ...)
///     {
///         var order = await orders.GetByIdAsync(request.OrderId, ct);
///         if (order is null) return AuthorizationResult.Success(); // handler returns NotFound
///         return currentUser.IsInRole("TenantAdmin") || order.CustomerId == currentUser.UserId
///             ? AuthorizationResult.Success()
///             : AuthorizationResult.Fail("You can only cancel your own orders.");
///     }
/// }
/// services.AddScoped&lt;IAuthorizer&lt;CancelOrderCommand&gt;, CancelOrderAuthorizer&gt;();
/// </code>
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IEnumerable<IAuthorizer<TRequest>> authorizers,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizerList = authorizers.ToList();

        if (authorizerList.Count == 0)
            return await next();

        var results = await Task.WhenAll(
            authorizerList.Select(a => a.AuthorizeAsync(request, cancellationToken)));

        var failures = results
            .Where(r => !r.IsAuthorized)
            .Select(r => r.FailureReason ?? "Unauthorized")
            .ToList();

        if (failures.Count == 0)
            return await next();

        var reason = string.Join("; ", failures);
        logger.LogWarning("Authorization failed for {Request}: {Reason}", typeof(TRequest).Name, reason);

        var error = Error.Forbidden("Authorization.Failed", reason);

        // Result and Result<T> — delegate to ResultHelper (cached reflection).
        // TResponse is only constrained to notnull here, so use the non-generic overload.
        if (typeof(TResponse).IsAssignableTo(typeof(Result)))
            return (TResponse)ResultHelper.Failure(typeof(TResponse), error);

        // Non-Result response type — cannot express authorization failure; log and continue.
        logger.LogError(
            "AuthorizationBehavior: TResponse {Type} is not a Result — authorization failure cannot be returned",
            typeof(TResponse).Name);
        return await next();
    }
}
