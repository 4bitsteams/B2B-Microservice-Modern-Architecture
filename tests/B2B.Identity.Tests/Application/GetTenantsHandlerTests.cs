using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetTenants;
using B2B.Identity.Domain.Entities;

namespace B2B.Identity.Tests.Application;

public sealed class GetTenantsHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadTenantRepository _tenantRepo = Substitute.For<IReadTenantRepository>();

    private readonly GetTenantsHandler _handler;

    public GetTenantsHandlerTests()
    {
        _handler = new GetTenantsHandler(_tenantRepo);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnAllTenants()
    {
        var tenants = new List<Tenant>
        {
            Tenant.Create("Acme Corp", "acme"),
            Tenant.Create("Beta Inc", "beta")
        };
        _tenantRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Tenant, bool>>>(), default)
            .Returns(tenants);

        var result = await _handler.Handle(new GetTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldMapTenantsToDtos()
    {
        var tenant = Tenant.Create("Acme Corp", "acme", "acme.com");
        _tenantRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Tenant, bool>>>(), default)
            .Returns(new List<Tenant> { tenant });

        var result = await _handler.Handle(new GetTenantsQuery(), default);

        result.Value!.Should().ContainSingle();
        var dto = result.Value[0];
        dto.Name.Should().Be("Acme Corp");
        dto.Slug.Should().Be("acme");
        dto.Domain.Should().Be("acme.com");
        dto.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WithNoTenants_ShouldReturnEmptyList()
    {
        _tenantRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Tenant, bool>>>(), default)
            .Returns(new List<Tenant>());

        var result = await _handler.Handle(new GetTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
