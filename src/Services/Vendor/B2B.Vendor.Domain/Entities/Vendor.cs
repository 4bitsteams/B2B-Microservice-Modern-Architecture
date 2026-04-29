using B2B.Vendor.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Vendor.Domain.Entities;

public sealed class Vendor : AggregateRoot<Guid>, IAuditableEntity
{
    public string CompanyName { get; private set; } = default!;
    public string ContactEmail { get; private set; } = default!;
    public string ContactPhone { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public VendorStatus Status { get; private set; }
    public Guid TenantId { get; private set; }
    public string? Website { get; private set; }
    public string? Description { get; private set; }
    public decimal? CommissionRate { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Vendor() { }

    public static Vendor Register(string companyName, string contactEmail, string taxId,
        string address, string city, string country, Guid tenantId,
        string? contactPhone = null, string? website = null, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxId);

        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            CompanyName = companyName,
            ContactEmail = contactEmail.ToLowerInvariant(),
            ContactPhone = contactPhone ?? string.Empty,
            TaxId = taxId,
            Address = address,
            City = city,
            Country = country,
            TenantId = tenantId,
            Website = website,
            Description = description,
            Status = VendorStatus.PendingApproval
        };

        vendor.RaiseDomainEvent(new VendorRegisteredEvent(vendor.Id, vendor.CompanyName, vendor.ContactEmail));
        return vendor;
    }

    public void Approve(decimal commissionRate)
    {
        if (Status != VendorStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve vendor in status '{Status}'.");
        if (commissionRate is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(commissionRate), "Commission rate must be 0-100.");

        Status = VendorStatus.Active;
        CommissionRate = commissionRate;
        RaiseDomainEvent(new VendorApprovedEvent(Id, CompanyName, CommissionRate.Value));
    }

    public void Suspend(string reason)
    {
        if (Status == VendorStatus.Suspended)
            throw new InvalidOperationException("Vendor is already suspended.");

        Status = VendorStatus.Suspended;
        RaiseDomainEvent(new VendorSuspendedEvent(Id, CompanyName, reason));
    }

    public void Reactivate()
    {
        if (Status != VendorStatus.Suspended)
            throw new InvalidOperationException("Only suspended vendors can be reactivated.");
        Status = VendorStatus.Active;
    }

    public void Deactivate()
    {
        if (Status == VendorStatus.Deactivated)
            throw new InvalidOperationException("Vendor is already deactivated.");
        Status = VendorStatus.Deactivated;
        RaiseDomainEvent(new VendorDeactivatedEvent(Id, CompanyName));
    }

    public void UpdateProfile(string companyName, string contactEmail, string? contactPhone,
        string address, string city, string country, string? website, string? description)
    {
        CompanyName = companyName;
        ContactEmail = contactEmail.ToLowerInvariant();
        ContactPhone = contactPhone ?? string.Empty;
        Address = address;
        City = city;
        Country = country;
        Website = website;
        Description = description;
    }
}

public enum VendorStatus { PendingApproval, Active, Suspended, Deactivated }
