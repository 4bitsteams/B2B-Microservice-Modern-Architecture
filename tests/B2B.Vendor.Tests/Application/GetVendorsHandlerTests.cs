using B2B.Vendor.Application.Interfaces;
using B2B.Vendor.Application.Queries.GetVendors;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Tests.Application;

public sealed class GetVendorsHandlerTests
{
    private readonly IReadVendorRepository _readRepo = Substitute.For<IReadVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetVendorsHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetVendorsHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetVendorsHandler(_readRepo, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedVendors()
    {
        var vendor = VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 St", "NYC", "US", TenantId);
        var paged = PagedList<VendorEntity>.Create([vendor], 1, 20, 1);
        _readRepo.GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _handler.Handle(new GetVendorsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task Handle_EmptyList_ShouldReturnEmptyPaged()
    {
        var paged = PagedList<VendorEntity>.Create([], 1, 20, 0);
        _readRepo.GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _handler.Handle(new GetVendorsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
