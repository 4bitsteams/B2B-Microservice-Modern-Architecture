using System.Linq.Expressions;
using B2B.Payment.Application.Interfaces;
using B2B.Payment.Application.Queries.GetPaymentsByOrder;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Tests.Application;

public sealed class GetPaymentsByOrderHandlerTests
{
    private readonly IReadPaymentRepository _repo = Substitute.For<IReadPaymentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetPaymentsByOrderHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public GetPaymentsByOrderHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetPaymentsByOrderHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NoPayments_ShouldReturnEmpty()
    {
        _repo.FindAsync(Arg.Any<Expression<Func<PaymentEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PaymentEntity>)Array.Empty<PaymentEntity>());

        var result = await _handler.Handle(new GetPaymentsByOrderQuery(OrderId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithPayments_ShouldMapToDtos()
    {
        var p1 = PaymentEntity.Create(OrderId, Guid.NewGuid(), TenantId, 50m, "USD", PaymentMethod.CreditCard);
        var p2 = PaymentEntity.Create(OrderId, Guid.NewGuid(), TenantId, 100m, "USD", PaymentMethod.BankTransfer);
        _repo.FindAsync(Arg.Any<Expression<Func<PaymentEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PaymentEntity>)new[] { p1, p2 });

        var result = await _handler.Handle(new GetPaymentsByOrderQuery(OrderId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Sum(x => x.Amount).Should().Be(150m);
    }
}
