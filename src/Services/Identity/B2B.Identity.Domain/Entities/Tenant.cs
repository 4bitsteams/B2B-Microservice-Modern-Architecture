using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Domain.Entities;

public sealed class Tenant : AggregateRoot<Guid>, IAuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Domain { get; private set; }
    public TenantStatus Status { get; private set; }
    public string? LogoUrl { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<User> _users = [];
    public IReadOnlyList<User> Users => _users.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(string name, string slug, string? domain = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            Domain = domain,
            Status = TenantStatus.Active
        };
    }

    public void Deactivate() => Status = TenantStatus.Inactive;
    public void Activate() => Status = TenantStatus.Active;
    public void SetLogo(string logoUrl) => LogoUrl = logoUrl;
    public void UpdateDomain(string domain) => Domain = domain;
}

public enum TenantStatus { Active, Inactive, Suspended }
