using B2B.Payment.Application.Commands.ProcessPayment;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Tests.Application;

public sealed class ProcessPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ProcessPaymentHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly ProcessPaymentCommand ValidCommand = new(
        OrderId: Guid.NewGuid(),
        Amount: 99.99m,
        Currency: "USD",
        Method: PaymentMethod.CreditCard);

    public ProcessPaymentHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _currentUser.TenantId.Returns(TenantId);
        _repo.GetByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PaymentEntity?)null);
        _handler = new ProcessPaymentHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnCompleted()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
        result.Value.TransactionReference.Should().StartWith("TXN-");
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        await _handler.Handle(ValidCommand, default);

        await _repo.Received(1).AddAsync(Arg.Any<PaymentEntity>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateOrder_ShouldReturnConflict()
    {
        var existing = PaymentEntity.Create(ValidCommand.OrderId, UserId, TenantId, 99.99m, "USD", PaymentMethod.CreditCard);
        _repo.GetByOrderIdAsync(ValidCommand.OrderId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Payment.AlreadyExists");
    }
}
