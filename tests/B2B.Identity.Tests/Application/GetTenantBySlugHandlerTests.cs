using FluentAssertions;
using NSubstitute;
using Xunit;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Application.Queries.GetTenantBySlug;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;

namespace B2B.Identity.Tests.Application;

public sealed class GetTenantBySlugHandlerTests
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly IReadTenantRepository _tenantRepo = Substitute.For<IReadTenantRepository>();

    private readonly GetTenantBySlugHandler _handler;

    public GetTenantBySlugHandlerTests()
    {
        _handler = new GetTenantBySlugHandler(_tenantRepo);
    }

    // ── Happy path ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTenantExists_ShouldReturnDto()
    {
        var tenant = Tenant.Create("Acme Corp", "acme", "acme.com");
        _tenantRepo.GetBySlugAsync("acme", default).Returns(tenant);

        var result = await _handler.Handle(new GetTenantBySlugQuery("acme"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Acme Corp");
        result.Value.Slug.Should().Be("acme");
        result.Value.Domain.Should().Be("acme.com");
    }

    // ── Error paths ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFound()
    {
        _tenantRepo.GetBySlugAsync("unknown", default).Returns((Tenant?)null);

        var result = await _handler.Handle(new GetTenantBySlugQuery("unknown"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldIncludeSlugInMessage()
    {
        _tenantRepo.GetBySlugAsync("missing-slug", default).Returns((Tenant?)null);

        var result = await _handler.Handle(new GetTenantBySlugQuery("missing-slug"), default);

        result.Error.Description.Should().Contain("missing-slug");
    }
}
