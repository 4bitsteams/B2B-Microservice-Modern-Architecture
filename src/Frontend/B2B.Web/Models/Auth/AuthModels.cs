namespace B2B.Web.Models.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string TenantName);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string Email,
    string Role,
    Guid UserId,
    Guid TenantId,
    string FirstName,
    string LastName);

public sealed record RegisterResponse(Guid UserId, string Email);

public sealed record AuthResult(
    bool Succeeded,
    string? Token,
    string? RefreshToken,
    string? Error = null,
    LoginResponse? User = null);
