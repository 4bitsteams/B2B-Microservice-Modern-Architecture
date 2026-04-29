using B2B.Payment.Application.Commands.MarkInvoicePaid;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Payment.Tests.Application;

public sealed class MarkInvoicePaidHandlerTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly MarkInvoicePaidHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public MarkInvoicePaidHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new MarkInvoicePaidHandler(_repo, _currentUser, _uow);
    }

    private Invoice IssuedInvoice() =>
        Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), TenantId, 100m, 0m, "USD");

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);

        var result = await _handler.Handle(new MarkInvoicePaidCommand(Guid.NewGuid(), "PAY-1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var inv = Invoice.Create("INV-X", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, 0m, "USD");
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new MarkInvoicePaidCommand(inv.Id, "PAY"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyPaid_ShouldReturnValidation()
    {
        var inv = IssuedInvoice();
        inv.MarkPaid("PAY-prior");
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new MarkInvoicePaidCommand(inv.Id, "PAY-2"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Invoice.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Valid_ShouldMarkPaid()
    {
        var inv = IssuedInvoice();
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new MarkInvoicePaidCommand(inv.Id, "PAY-1"), default);

        result.IsSuccess.Should().BeTrue();
        inv.Status.Should().Be(InvoiceStatus.Paid);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
