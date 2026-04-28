using B2B.Identity.Domain.Entities;

namespace B2B.Identity.Application.Interfaces;

/// <summary>
/// Generates and validates JWT access tokens and opaque refresh tokens.
///
/// The Infrastructure implementation (<c>TokenService</c>) uses
/// <see cref="Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler"/> —
/// the recommended handler from .NET 7 onwards — and is registered as a
/// <b>singleton</b> because all fields are immutable after construction and
/// the handler is thread-safe.
///
/// Access tokens carry the standard JWT claims plus <c>tenant_id</c> and
/// <c>roles</c> so downstream services can resolve <c>ICurrentUser</c> without
/// a database round-trip.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT access token for <paramref name="user"/> embedding the
    /// provided <paramref name="roles"/> as <c>ClaimTypes.Role</c> claims.
    /// </summary>
    /// <param name="user">The authenticated user whose identity is encoded in the token.</param>
    /// <param name="roles">Role names to embed as role claims.</param>
    /// <returns>A compact-serialized JWT string.</returns>
    string GenerateAccessToken(User user, IEnumerable<string> roles);

    /// <summary>
    /// Generates a cryptographically random opaque refresh token (Base64-encoded
    /// 64-byte value). Stored hashed in the database; never exposed after the
    /// initial login/refresh response.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates the signature and claims of <paramref name="token"/> and extracts
    /// the embedded user and tenant identifiers.
    /// Lifetime validation is intentionally <b>disabled</b> — expired access tokens
    /// are still valid during the refresh flow.
    /// </summary>
    /// <param name="token">The compact-serialized JWT to validate.</param>
    /// <returns>A tuple of <c>(userId, tenantId)</c> extracted from the token claims.</returns>
    /// <exception cref="Microsoft.IdentityModel.Tokens.SecurityTokenException">
    /// Thrown when the token signature is invalid or required claims are missing.
    /// </exception>
    (Guid userId, Guid tenantId) ValidateToken(string token);
}
