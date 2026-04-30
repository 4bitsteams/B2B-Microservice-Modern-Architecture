using B2B.Vendor.Domain.Events;
using FluentAssertions;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;
using VendorStatus = B2B.Vendor.Domain.Entities.VendorStatus;

namespace B2B.Vendor.Tests.Domain;

public sealed class VendorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static VendorEntity NewPending() =>
        VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-12345",
            "1 Main St", "New York", "US", TenantId);

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public void Register_ValidData_ShouldInitializePendingApproval()
    {
        var vendor = NewPending();

        vendor.Status.Should().Be(VendorStatus.PendingApproval);
        vendor.CompanyName.Should().Be("Acme Corp");
        vendor.ContactEmail.Should().Be("contact@acme.com");
        vendor.TaxId.Should().Be("TX-12345");
        vendor.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Register_ShouldRaiseVendorRegisteredEvent()
    {
        var vendor = NewPending();

        vendor.DomainEvents.Should().ContainSingle(e => e is VendorRegisteredEvent);
        var ev = (VendorRegisteredEvent)vendor.DomainEvents.Single();
        ev.CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public void Register_EmailShouldBeLowercased()
    {
        var vendor = VendorEntity.Register("Acme", "Contact@ACME.COM", "TX-1",
            "1 St", "NYC", "US", TenantId);

        vendor.ContactEmail.Should().Be("contact@acme.com");
    }

    [Fact]
    public void Register_BlankCompanyName_ShouldThrow()
    {
        var act = () => VendorEntity.Register("", "e@e.com", "TX-1", "1 St", "NYC", "US", TenantId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_BlankEmail_ShouldThrow()
    {
        var act = () => VendorEntity.Register("Acme", "", "TX-1", "1 St", "NYC", "US", TenantId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_BlankTaxId_ShouldThrow()
    {
        var act = () => VendorEntity.Register("Acme", "e@e.com", "", "1 St", "NYC", "US", TenantId);
        act.Should().Throw<ArgumentException>();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromPending_ShouldMakeActive()
    {
        var vendor = NewPending();
        vendor.ClearDomainEvents();

        vendor.Approve(10m);

        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.CommissionRate.Should().Be(10m);
        vendor.DomainEvents.Should().ContainSingle(e => e is VendorApprovedEvent);
    }

    [Fact]
    public void Approve_AlreadyActive_ShouldThrow()
    {
        var vendor = NewPending();
        vendor.Approve(5m);

        var act = () => vendor.Approve(10m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_NegativeCommission_ShouldThrow()
    {
        var vendor = NewPending();

        var act = () => vendor.Approve(-1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Approve_CommissionOver100_ShouldThrow()
    {
        var vendor = NewPending();

        var act = () => vendor.Approve(101m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Suspend ───────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_ActiveVendor_ShouldMakeSuspended()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        vendor.ClearDomainEvents();

        vendor.Suspend("Policy violation");

        vendor.Status.Should().Be(VendorStatus.Suspended);
        vendor.DomainEvents.Should().ContainSingle(e => e is VendorSuspendedEvent);
    }

    [Fact]
    public void Suspend_AlreadySuspended_ShouldThrow()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        vendor.Suspend("First reason");

        var act = () => vendor.Suspend("Second reason");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Reactivate ────────────────────────────────────────────────────────────

    [Fact]
    public void Reactivate_SuspendedVendor_ShouldMakeActive()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        vendor.Suspend("Reason");

        vendor.Reactivate();

        vendor.Status.Should().Be(VendorStatus.Active);
    }

    [Fact]
    public void Reactivate_ActiveVendor_ShouldThrow()
    {
        var vendor = NewPending();
        vendor.Approve(5m);

        var act = () => vendor.Reactivate();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ActiveVendor_ShouldMakeDeactivated()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        vendor.ClearDomainEvents();

        vendor.Deactivate();

        vendor.Status.Should().Be(VendorStatus.Deactivated);
        vendor.DomainEvents.Should().ContainSingle(e => e is VendorDeactivatedEvent);
    }

    [Fact]
    public void Deactivate_AlreadyDeactivated_ShouldThrow()
    {
        var vendor = NewPending();
        vendor.Approve(5m);
        vendor.Deactivate();

        var act = () => vendor.Deactivate();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── UpdateProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void UpdateProfile_ShouldChangeFields()
    {
        var vendor = NewPending();

        vendor.UpdateProfile("New Name", "new@example.com", "+1234", "2 St", "LA", "US", "https://new.com", "desc");

        vendor.CompanyName.Should().Be("New Name");
        vendor.ContactEmail.Should().Be("new@example.com");
        vendor.City.Should().Be("LA");
        vendor.Website.Should().Be("https://new.com");
    }
}
