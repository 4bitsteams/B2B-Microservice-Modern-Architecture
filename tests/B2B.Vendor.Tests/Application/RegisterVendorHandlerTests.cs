using B2B.Vendor.Application.Commands.RegisterVendor;
using B2B.Vendor.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using VendorEntity = B2B.Vendor.Domain.Entities.Vendor;

namespace B2B.Vendor.Tests.Application;

public sealed class RegisterVendorHandlerTests
{
    private readonly IVendorRepository _repo = Substitute.For<IVendorRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RegisterVendorHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly RegisterVendorCommand ValidCommand = new(
        CompanyName: "Acme Corp",
        ContactEmail: "contact@acme.com",
        TaxId: "TX-12345",
        Address: "1 Main St",
        City: "New York",
        Country: "US");

    public RegisterVendorHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByEmailAsync(ValidCommand.ContactEmail, TenantId, Arg.Any<CancellationToken>())
            .Returns((VendorEntity?)null);
        _repo.GetByTaxIdAsync(ValidCommand.TaxId, TenantId, Arg.Any<CancellationToken>())
            .Returns((VendorEntity?)null);
        _handler = new RegisterVendorHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnPendingApproval()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Acme Corp");
        result.Value.Status.Should().Be("PendingApproval");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<VendorEntity>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldReturnConflict()
    {
        var existing = VendorEntity.Register("Old Corp", "contact@acme.com", "TX-999",
            "1 St", "NYC", "US", TenantId);
        _repo.GetByEmailAsync(ValidCommand.ContactEmail, TenantId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vendor.EmailExists");
    }

    [Fact]
    public async Task Handle_DuplicateTaxId_ShouldReturnConflict()
    {
        var existing = VendorEntity.Register("Old Corp", "other@example.com", "TX-12345",
            "1 St", "NYC", "US", TenantId);
        _repo.GetByTaxIdAsync(ValidCommand.TaxId, TenantId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vendor.TaxIdExists");
    }
}
