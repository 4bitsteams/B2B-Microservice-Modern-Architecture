using B2B.Vendor.Application.Interfaces;
using B2B.Vendor.Application.Queries.GetVendorById;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Tests.Application;

public sealed class GetVendorByIdHandlerTests
{
    private readonly IReadVendorRepository _readRepo = Substitute.For<IReadVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetVendorByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetVendorByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetVendorByIdHandler(_readRepo, _currentUser);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _readRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((VendorEntity?)null);

        var result = await _handler.Handle(new GetVendorByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var vendor = VendorEntity.Register("Acme", "a@a.com", "TX-1", "1 St", "NYC", "US", Guid.NewGuid());
        _readRepo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new GetVendorByIdQuery(vendor.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Found_ShouldReturnVendorDetail()
    {
        var vendor = VendorEntity.Register("Acme Corp", "contact@acme.com", "TX-1",
            "1 Main St", "New York", "US", TenantId, "+1234", "https://acme.com", "Tech vendor");
        _readRepo.GetByIdAsync(vendor.Id, Arg.Any<CancellationToken>()).Returns(vendor);

        var result = await _handler.Handle(new GetVendorByIdQuery(vendor.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Acme Corp");
        result.Value.ContactEmail.Should().Be("contact@acme.com");
        result.Value.City.Should().Be("New York");
        result.Value.Status.Should().Be("PendingApproval");
        result.Value.Website.Should().Be("https://acme.com");
    }
}
