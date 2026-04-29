using B2B.Payment.Application.Commands.CreateInvoice;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Payment.Tests.Application;

public sealed class CreateInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateInvoiceHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly CreateInvoiceCommand ValidCommand = new(
        OrderId: Guid.NewGuid(),
        Subtotal: 100m,
        TaxAmount: 7m,
        Currency: "USD",
        NetTermsDays: 30);

    public CreateInvoiceHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByOrderIdAsync(ValidCommand.OrderId, Arg.Any<CancellationToken>()).Returns((Invoice?)null);
        _repo.GenerateInvoiceNumberAsync(Arg.Any<CancellationToken>()).Returns("INV-2026-001");
        _handler = new CreateInvoiceHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnSuccess()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.InvoiceNumber.Should().Be("INV-2026-001");
        result.Value.TotalAmount.Should().Be(107m);
    }

    [Fact]
    public async Task Handle_DuplicateOrder_ShouldReturnConflict()
    {
        var existing = Invoice.Create("INV-X", ValidCommand.OrderId, UserId, TenantId, 50m, 0m, "USD");
        _repo.GetByOrderIdAsync(ValidCommand.OrderId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Invoice.AlreadyExists");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
