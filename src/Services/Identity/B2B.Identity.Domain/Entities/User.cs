using B2B.Identity.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Domain.Entities;

public sealed class User : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = default!;
    public UserStatus Status { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles.AsReadOnly();

    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";
    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;

    private User() { }

    public static User Create(
        string firstName, string lastName, string email,
        string passwordHash, Guid tenantId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            TenantId = tenantId,
            Status = UserStatus.Active
        };

        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, user.Email, tenantId));
        return user;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
            LockedUntil = DateTime.UtcNow.AddMinutes(15);
    }

    private const int MaxActiveRefreshTokens = 5;

    public void AddRefreshToken(string token, DateTime expiry)
    {
        // Prune expired / revoked tokens before adding to prevent unbounded
        // row growth in the RefreshTokens table as users continually refresh.
        _refreshTokens.RemoveAll(t => !t.IsActive);

        // Revoke the oldest active tokens if still over the limit
        // (e.g. user is logged in from many devices simultaneously).
        while (_refreshTokens.Count >= MaxActiveRefreshTokens)
        {
            var oldest = _refreshTokens.MinBy(t => t.CreatedAt);
            if (oldest is not null) oldest.Revoke();
            _refreshTokens.Remove(oldest!);
        }

        _refreshTokens.Add(RefreshToken.Create(Id, token, expiry));
    }

    public void RevokeRefreshToken(string token)
    {
        var refreshToken = _refreshTokens.FirstOrDefault(t => t.Token == token);
        refreshToken?.Revoke();
    }

    public void RevokeAllRefreshTokens() =>
        _refreshTokens.ForEach(t => t.Revoke());

    public void AssignRole(Guid roleId) =>
        _userRoles.Add(new UserRole { UserId = Id, RoleId = roleId });

    public void VerifyEmail()
    {
        EmailVerified = true;
        RaiseDomainEvent(new UserEmailVerifiedEvent(Id, Email));
    }

    public void UpdatePassword(string newPasswordHash) => PasswordHash = newPasswordHash;

    public void UpdateProfile(string firstName, string lastName, string phoneNumber)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }

    public void Deactivate() => Status = UserStatus.Inactive;
    public void Activate() => Status = UserStatus.Active;
}

public enum UserStatus { Active, Inactive, Suspended }
