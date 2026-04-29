using B2B.Payment.Application.Commands.RefundPayment;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Tests.Application;

public sealed class RefundPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RefundPaymentHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public RefundPaymentHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new RefundPaymentHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PaymentEntity?)null);

        var result = await _handler.Handle(new RefundPaymentCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var p = PaymentEntity.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", PaymentMethod.CreditCard);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var result = await _handler.Handle(new RefundPaymentCommand(p.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Pending_ShouldReturnValidation()
    {
        var p = PaymentEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TenantId, 50m, "USD", PaymentMethod.CreditCard);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var result = await _handler.Handle(new RefundPaymentCommand(p.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Payment.InvalidStatus");
    }

    [Fact]
    public async Task Handle_Completed_ShouldRefundAndPersist()
    {
        var p = PaymentEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TenantId, 50m, "USD", PaymentMethod.CreditCard);
        p.Process("TXN");
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var result = await _handler.Handle(new RefundPaymentCommand(p.Id), default);

        result.IsSuccess.Should().BeTrue();
        p.Status.Should().Be(PaymentStatus.Refunded);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
