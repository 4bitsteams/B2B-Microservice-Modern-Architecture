using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;

namespace B2B.Identity.Infrastructure.Services;

/// <summary>
/// JWT token service.
///
/// Key design decisions:
///   • <see cref="JsonWebTokenHandler"/> replaces the older <see cref="JwtSecurityTokenHandler"/>
///     — it avoids XML-DOM overhead and is the recommended handler from .NET 7 onwards.
///   • <see cref="SigningCredentials"/> and <see cref="TokenValidationParameters"/> are built
///     once in the constructor so hot-path calls (login, refresh) do not re-parse config or
///     re-allocate crypto objects on every request.
///   • <see cref="RandomNumberGenerator.Fill"/> is used instead of creating a disposable
///     <see cref="RandomNumberGenerator"/> instance per refresh-token generation.
///   • The service should be registered as a <b>singleton</b> — all fields are immutable
///     after construction and <see cref="JsonWebTokenHandler"/> is thread-safe.
/// </summary>
public sealed class TokenService : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly string _issuer;
    private readonly string _audience;
    private readonly double _expiryMinutes;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public TokenService(IConfiguration config)
    {
        var jwt = config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!));

        _issuer        = jwt["Issuer"]!;
        _audience      = jwt["Audience"]!;
        _expiryMinutes = double.Parse(jwt["ExpiryMinutes"] ?? "60");

        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Shared across all ValidateToken calls — no re-allocation on hot path.
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = key,
            ValidateIssuer           = true,
            ValidIssuer              = _issuer,
            ValidateAudience         = true,
            ValidAudience            = _audience,
            ValidateLifetime         = false   // Allow expired tokens for refresh-token flow
        };
    }

    public string GenerateAccessToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.GivenName,      user.FirstName),
            new(ClaimTypes.Surname,        user.LastName),
            new("tenant_id",               user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = DateTime.UtcNow.AddMinutes(_expiryMinutes),
            Issuer             = _issuer,
            Audience           = _audience,
            SigningCredentials = _signingCredentials
        };

        return Handler.CreateToken(descriptor);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);   // static — no allocation, no disposal
        return Convert.ToBase64String(bytes);
    }

    public (Guid userId, Guid tenantId) ValidateToken(string token)
    {
        try
        {
            // ValidateTokenAsync is CPU-bound here (signature verify, no I/O) because we
            // supply the signing key directly — blocking is safe.
            var result = Handler.ValidateTokenAsync(token, _validationParameters)
                .GetAwaiter().GetResult();

            if (!result.IsValid)
                throw new SecurityTokenException("Invalid token.");

            var identity = result.ClaimsIdentity;
            var userId   = Guid.Parse(identity.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var tenantId = Guid.Parse(identity.FindFirst("tenant_id")!.Value);
            return (userId, tenantId);
        }
        catch (Exception ex) when (ex is not SecurityTokenException)
        {
            throw new SecurityTokenException("Invalid token.");
        }
    }
}
