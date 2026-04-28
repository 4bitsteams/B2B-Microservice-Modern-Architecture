using FluentValidation;
using MediatR;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);

        // Run all validators concurrently (supports async DB-backed validators
        // such as uniqueness checks) without blocking thread-pool threads.
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var error = Error.Validation(
            failures[0].PropertyName,
            failures[0].ErrorMessage);

        return ResultHelper.Failure<TResponse>(error);
    }
}
