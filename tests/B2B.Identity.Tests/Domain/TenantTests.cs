using FluentAssertions;
using Xunit;
using B2B.Identity.Domain.Entities;

namespace B2B.Identity.Tests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        var tenant = Tenant.Create("Acme Corp", "acme", "acme.com");

        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme");
        tenant.Domain.Should().Be("acme.com");
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldNormalizeSlugToLowercase()
    {
        var tenant = Tenant.Create("Acme Corp", "ACME-CORP");

        tenant.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public void Create_WithoutDomain_ShouldSucceed()
    {
        var tenant = Tenant.Create("Acme Corp", "acme");

        tenant.Domain.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        var act = () => Tenant.Create("", "acme");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceName_ShouldThrow()
    {
        var act = () => Tenant.Create("   ", "acme");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptySlug_ShouldThrow()
    {
        var act = () => Tenant.Create("Acme", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceSlug_ShouldThrow()
    {
        var act = () => Tenant.Create("Acme", "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ShouldSetStatusInactive()
    {
        var tenant = Tenant.Create("Acme", "acme");
        tenant.Deactivate();
        tenant.Status.Should().Be(TenantStatus.Inactive);
    }

    [Fact]
    public void Activate_ShouldRestoreActiveStatus()
    {
        var tenant = Tenant.Create("Acme", "acme");
        tenant.Deactivate();
        tenant.Activate();
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void SetLogo_ShouldUpdateLogoUrl()
    {
        var tenant = Tenant.Create("Acme", "acme");
        tenant.SetLogo("https://cdn.example.com/logo.png");
        tenant.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
    }

    [Fact]
    public void UpdateDomain_ShouldChangeDomain()
    {
        var tenant = Tenant.Create("Acme", "acme", "old.com");
        tenant.UpdateDomain("new.com");
        tenant.Domain.Should().Be("new.com");
    }
}
