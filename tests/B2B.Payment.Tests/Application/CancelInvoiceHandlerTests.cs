using B2B.Payment.Application.Commands.CancelInvoice;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Payment.Tests.Application;

public sealed class CancelInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CancelInvoiceHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public CancelInvoiceHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new CancelInvoiceHandler(_repo, _currentUser, _uow);
    }

    private Invoice IssuedInvoice() =>
        Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), TenantId, 100m, 0m, "USD");

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);

        var result = await _handler.Handle(new CancelInvoiceCommand(Guid.NewGuid(), "test"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyPaid_ShouldReturnConflict()
    {
        var inv = IssuedInvoice();
        inv.MarkPaid("PAY");
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new CancelInvoiceCommand(inv.Id, "oops"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Invoice.InvalidState");
    }

    [Fact]
    public async Task Handle_Valid_ShouldCancel()
    {
        var inv = IssuedInvoice();
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new CancelInvoiceCommand(inv.Id, "customer request"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
