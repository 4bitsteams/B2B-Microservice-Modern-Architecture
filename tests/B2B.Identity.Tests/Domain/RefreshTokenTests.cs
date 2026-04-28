using FluentAssertions;
using Xunit;
using B2B.Identity.Domain.Entities;

namespace B2B.Identity.Tests.Domain;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var userId = Guid.NewGuid();
        var expiry = DateTime.UtcNow.AddDays(7);

        var token = RefreshToken.Create(userId, "my-token", expiry);

        token.Id.Should().NotBeEmpty();
        token.UserId.Should().Be(userId);
        token.Token.Should().Be("my-token");
        token.ExpiresAt.Should().Be(expiry);
        token.IsRevoked.Should().BeFalse();
        token.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsActive_WhenFreshAndNotRevoked_ShouldBeTrue()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "t", DateTime.UtcNow.AddDays(7));

        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenRevoked_ShouldBeFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "t", DateTime.UtcNow.AddDays(7));

        token.Revoke();

        token.IsActive.Should().BeFalse();
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldBeFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "t", DateTime.UtcNow.AddSeconds(-1));

        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WithReplacementToken_ShouldStoreReplacedByToken()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "old-token", DateTime.UtcNow.AddDays(7));

        token.Revoke("new-token");

        token.IsRevoked.Should().BeTrue();
        token.ReplacedByToken.Should().Be("new-token");
    }

    [Fact]
    public void Revoke_WithoutReplacementToken_ShouldLeaveReplacedByTokenNull()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "t", DateTime.UtcNow.AddDays(7));

        token.Revoke();

        token.ReplacedByToken.Should().BeNull();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsExactlyNow_ShouldBeTrue()
    {
        // ExpiresAt == UtcNow → IsExpired = (UtcNow >= ExpiresAt) = true
        var token = RefreshToken.Create(Guid.NewGuid(), "t", DateTime.UtcNow.AddMilliseconds(-1));

        token.IsExpired.Should().BeTrue();
    }
}
