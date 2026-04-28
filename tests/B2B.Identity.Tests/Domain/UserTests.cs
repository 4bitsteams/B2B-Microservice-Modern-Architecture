using FluentAssertions;
using Xunit;
using B2B.Identity.Domain.Entities;
using B2B.Identity.Domain.Events;

namespace B2B.Identity.Tests.Domain;

public sealed class UserTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static User CreateUser() =>
        User.Create("Jane", "Doe", "jane.doe@example.com", "hashed_pw", TenantId);

    // ──────────────────────────────────────────────
    // Factory / creation
    // ──────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        var user = CreateUser();

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be("jane.doe@example.com");
        user.PasswordHash.Should().Be("hashed_pw");
        user.TenantId.Should().Be(TenantId);
        user.Status.Should().Be(UserStatus.Active);
        user.EmailVerified.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldNormalizeEmailToLowercase()
    {
        var user = User.Create("Jane", "Doe", "Jane.DOE@Example.COM", "pw", TenantId);

        user.Email.Should().Be("jane.doe@example.com");
    }

    [Fact]
    public void Create_ShouldRaiseUserRegisteredEvent()
    {
        var user = CreateUser();

        user.DomainEvents.Should().ContainSingle(e => e is UserRegisteredEvent);
        var evt = (UserRegisteredEvent)user.DomainEvents[0];
        evt.UserId.Should().Be(user.Id);
        evt.Email.Should().Be(user.Email);
        evt.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void FullName_ShouldCombineFirstAndLastName()
    {
        var user = CreateUser();
        user.FullName.Should().Be("Jane Doe");
    }

    // ──────────────────────────────────────────────
    // Login tracking
    // ──────────────────────────────────────────────

    [Fact]
    public void RecordLogin_ShouldResetFailedAttemptsAndUpdateLastLoginAt()
    {
        var user = CreateUser();
        user.RecordFailedLogin();

        user.RecordLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void RecordFailedLogin_BelowFiveAttempts_ShouldIncrementCounterWithoutLocking(int attempts)
    {
        var user = CreateUser();
        for (var i = 0; i < attempts; i++) user.RecordFailedLogin();

        user.FailedLoginAttempts.Should().Be(attempts);
        user.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_AtFiveAttempts_ShouldLockAccount()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();

        user.IsLocked.Should().BeTrue();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void IsLocked_WhenLockedUntilInFuture_ShouldBeTrue()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();

        user.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void IsLocked_WhenNeverLocked_ShouldBeFalse()
    {
        var user = CreateUser();
        user.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void RecordLogin_AfterLockout_ShouldUnlockAccount()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();
        user.IsLocked.Should().BeTrue();

        user.RecordLogin();

        user.IsLocked.Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // Email verification
    // ──────────────────────────────────────────────

    [Fact]
    public void VerifyEmail_ShouldSetEmailVerifiedTrue()
    {
        var user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail();

        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public void VerifyEmail_ShouldRaiseUserEmailVerifiedEvent()
    {
        var user = CreateUser();
        user.ClearDomainEvents();

        user.VerifyEmail();

        user.DomainEvents.Should().ContainSingle(e => e is UserEmailVerifiedEvent);
        var evt = (UserEmailVerifiedEvent)user.DomainEvents[0];
        evt.UserId.Should().Be(user.Id);
        evt.Email.Should().Be(user.Email);
    }

    // ──────────────────────────────────────────────
    // Status
    // ──────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetStatusToInactive()
    {
        var user = CreateUser();
        user.Deactivate();
        user.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public void Activate_ShouldSetStatusToActive()
    {
        var user = CreateUser();
        user.Deactivate();
        user.Activate();
        user.Status.Should().Be(UserStatus.Active);
    }

    // ──────────────────────────────────────────────
    // Role assignment
    // ──────────────────────────────────────────────

    [Fact]
    public void AssignRole_ShouldAddUserRoleEntry()
    {
        var user = CreateUser();
        var roleId = Guid.NewGuid();

        user.AssignRole(roleId);

        user.UserRoles.Should().ContainSingle(ur => ur.RoleId == roleId && ur.UserId == user.Id);
    }

    [Fact]
    public void AssignRole_MultipleTimes_ShouldAddMultipleEntries()
    {
        var user = CreateUser();
        user.AssignRole(Guid.NewGuid());
        user.AssignRole(Guid.NewGuid());

        user.UserRoles.Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────
    // Refresh tokens
    // ──────────────────────────────────────────────

    [Fact]
    public void AddRefreshToken_ShouldAppendToken()
    {
        var user = CreateUser();

        user.AddRefreshToken("token-abc", DateTime.UtcNow.AddDays(7));

        user.RefreshTokens.Should().ContainSingle(t => t.Token == "token-abc");
    }

    [Fact]
    public void AddRefreshToken_ShouldPruneExpiredTokensBeforeAdding()
    {
        var user = CreateUser();
        // Add 3 already-expired tokens
        user.AddRefreshToken("expired-1", DateTime.UtcNow.AddDays(-1));
        user.AddRefreshToken("expired-2", DateTime.UtcNow.AddDays(-1));
        user.AddRefreshToken("expired-3", DateTime.UtcNow.AddDays(-1));

        // Adding a fresh token should prune the expired ones first
        user.AddRefreshToken("fresh", DateTime.UtcNow.AddDays(7));

        user.RefreshTokens.Should().ContainSingle(t => t.Token == "fresh");
    }

    [Fact]
    public void AddRefreshToken_WhenAtMaxActiveLimit_ShouldRevokeOldest()
    {
        var user = CreateUser();
        var expiry = DateTime.UtcNow.AddDays(7);

        // Fill to the max limit (5)
        for (var i = 1; i <= 5; i++)
            user.AddRefreshToken($"token-{i}", expiry);

        // Adding a 6th should push out the oldest
        user.AddRefreshToken("token-6", expiry);

        user.RefreshTokens.Should().HaveCountLessOrEqualTo(5);
        user.RefreshTokens.Should().Contain(t => t.Token == "token-6");
    }

    [Fact]
    public void RevokeRefreshToken_ShouldMarkTokenAsRevoked()
    {
        var user = CreateUser();
        user.AddRefreshToken("my-token", DateTime.UtcNow.AddDays(7));

        user.RevokeRefreshToken("my-token");

        user.RefreshTokens.First(t => t.Token == "my-token").IsActive.Should().BeFalse();
    }

    [Fact]
    public void RevokeRefreshToken_WithNonExistentToken_ShouldNotThrow()
    {
        var user = CreateUser();

        var act = () => user.RevokeRefreshToken("does-not-exist");

        act.Should().NotThrow();
    }

    [Fact]
    public void RevokeAllRefreshTokens_ShouldRevokeEveryToken()
    {
        var user = CreateUser();
        var expiry = DateTime.UtcNow.AddDays(7);
        user.AddRefreshToken("t1", expiry);
        user.AddRefreshToken("t2", expiry);
        user.AddRefreshToken("t3", expiry);

        user.RevokeAllRefreshTokens();

        user.RefreshTokens.Should().OnlyContain(t => !t.IsActive);
    }
}
