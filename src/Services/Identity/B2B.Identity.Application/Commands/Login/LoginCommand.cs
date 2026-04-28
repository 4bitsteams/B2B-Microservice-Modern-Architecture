using System.Text.Json.Serialization;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Commands.Login;

public sealed record LoginCommand(
    string Email,
    [property: JsonIgnore] string Password,   // excluded from AuditBehavior serialization
    string TenantSlug) : ICommand<LoginResponse>;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string FullName,
    IReadOnlyList<string> Roles);
