using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using B2B.Identity.Domain.Entities;
using B2B.Identity.Infrastructure.Services;
using System.Text;

namespace B2B.Identity.Tests.Infrastructure;

public sealed class TokenServiceTests
{
    // A symmetric key of at least 256 bits is required by HmacSha256.
    private const string SecretKey = "SuperSecretKeyForTestingPurposes_AtLeast32Chars!";

    private readonly TokenService _tokenService;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    public TokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = SecretKey,
                ["JwtSettings:Issuer"] = "B2B.Identity.Tests",
                ["JwtSettings:Audience"] = "B2B.Platform.Tests",
                ["JwtSettings:ExpiryMinutes"] = "60"
            })
            .Build();

        _tokenService = new TokenService(config);
    }

    private static User MakeUser() =>
        User.Create("Jane", "Doe", "jane@acme.com", "pw", TenantId);

    // ──────────────────────────────────────────────
    // Access token
    // ──────────────────────────────────────────────

    [Fact]
    public void GenerateAccessToken_ShouldReturnNonEmptyString()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, [Role.SystemRoles.User]);
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateAccessToken_ShouldBeValidJwtFormat()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);

        // A JWT has exactly 3 dot-separated segments
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUserIdClaim()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);

        var parsed = ParseToken(token);
        var sub = parsed.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
        Guid.Parse(sub).Should().Be(user.Id);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainTenantIdClaim()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);

        var parsed = ParseToken(token);
        var tenantClaim = parsed.Claims.First(c => c.Type == "tenant_id").Value;
        Guid.Parse(tenantClaim).Should().Be(TenantId);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeAllRoles()
    {
        var user = MakeUser();
        var roles = new[] { Role.SystemRoles.TenantAdmin, Role.SystemRoles.User };

        var token = _tokenService.GenerateAccessToken(user, roles);

        var parsed = ParseToken(token);
        var roleClaims = parsed.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectIssuerAndAudience()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);

        var parsed = ParseToken(token);
        parsed.Issuer.Should().Be("B2B.Identity.Tests");
        parsed.Audiences.Should().Contain("B2B.Platform.Tests");
    }

    [Fact]
    public void GenerateAccessToken_ShouldExpireInAboutSixtyMinutes()
    {
        var user = MakeUser();
        var before = DateTime.UtcNow;

        var token = _tokenService.GenerateAccessToken(user, []);

        var parsed = ParseToken(token);
        parsed.ValidTo.Should().BeCloseTo(before.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    // ──────────────────────────────────────────────
    // Refresh token
    // ──────────────────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        var token = _tokenService.GenerateRefreshToken();
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64EncodedString()
    {
        var token = _tokenService.GenerateRefreshToken();

        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueTokensOnEachCall()
    {
        var t1 = _tokenService.GenerateRefreshToken();
        var t2 = _tokenService.GenerateRefreshToken();

        t1.Should().NotBe(t2);
    }

    // ──────────────────────────────────────────────
    // ValidateToken
    // ──────────────────────────────────────────────

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnCorrectUserAndTenantId()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);

        var (returnedUserId, returnedTenantId) = _tokenService.ValidateToken(token);

        returnedUserId.Should().Be(user.Id);
        returnedTenantId.Should().Be(TenantId);
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ShouldThrowSecurityTokenException()
    {
        var user = MakeUser();
        var token = _tokenService.GenerateAccessToken(user, []);
        var tampered = token[..^5] + "XXXXX"; // corrupt signature

        var act = () => _tokenService.ValidateToken(tampered);

        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void ValidateToken_WithCompletelyInvalidString_ShouldThrow()
    {
        var act = () => _tokenService.ValidateToken("not.a.jwt");

        act.Should().Throw<Exception>();
    }

    // ValidateToken allows expired tokens (ValidateLifetime = false) so that
    // the refresh flow can still read claims from an expired access token.
    [Fact]
    public void ValidateToken_WithExpiredToken_ShouldStillReturnClaims()
    {
        // Build a token that was already expired at generation time using a
        // raw JwtSecurityToken so we can control the expiry without touching
        // production code.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: "B2B.Identity.Tests",
            audience: "B2B.Platform.Tests",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim("tenant_id", TenantId.ToString())
            ],
            expires: DateTime.UtcNow.AddSeconds(-1),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var (returnedUserId, returnedTenantId) = _tokenService.ValidateToken(tokenString);

        returnedUserId.Should().Be(UserId);
        returnedTenantId.Should().Be(TenantId);
    }

    // ──────────────────────────────────────────────
    // Helper
    // ──────────────────────────────────────────────

    private static JwtSecurityToken ParseToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));

        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = "B2B.Identity.Tests",
            ValidateAudience = true,
            ValidAudience = "B2B.Platform.Tests",
            ValidateLifetime = true
        }, out var validated);

        return (JwtSecurityToken)validated;
    }
}
