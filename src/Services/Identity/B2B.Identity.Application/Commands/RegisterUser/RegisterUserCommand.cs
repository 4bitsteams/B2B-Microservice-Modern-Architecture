using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string TenantSlug) : ICommand<RegisterUserResponse>;

public sealed record RegisterUserResponse(Guid UserId, string Email, string FullName);
