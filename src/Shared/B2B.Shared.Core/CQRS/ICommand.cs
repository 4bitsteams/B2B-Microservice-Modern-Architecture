using MediatR;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Core.CQRS;

/// <summary>
/// Marker interface for commands that mutate state and return no value.
/// Implement via <see cref="ICommandHandler{TCommand}"/>.
/// </summary>
public interface ICommand : IRequest<Result>;

/// <summary>
/// Marker interface for commands that mutate state and return a typed response.
/// Implement via <see cref="ICommandHandler{TCommand, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The type returned on success.</typeparam>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>
/// MediatR handler contract for commands that return no value.
/// Handlers should be registered as <c>Scoped</c> and injected through
/// the MediatR pipeline (Logging → Validation → DomainEvent → Handler).
/// </summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// MediatR handler contract for commands that return a typed <typeparamref name="TResponse"/>.
/// Handlers should be registered as <c>Scoped</c> and injected through
/// the MediatR pipeline (Logging → Validation → DomainEvent → Handler).
/// </summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
/// <typeparam name="TResponse">The type returned on success, wrapped in <see cref="Result{TResponse}"/>.</typeparam>
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
