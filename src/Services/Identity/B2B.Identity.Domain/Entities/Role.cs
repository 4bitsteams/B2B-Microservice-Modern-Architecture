using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Domain.Entities;

public sealed class Role : AggregateRoot<Guid>, IAuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles.AsReadOnly();

    private Role() { }

    public static Role Create(string name, string description = "", bool isSystem = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsSystem = isSystem
        };

    public static class SystemRoles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string TenantAdmin = "TenantAdmin";
        public const string User = "User";
        public const string ReadOnly = "ReadOnly";
    }
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
}
