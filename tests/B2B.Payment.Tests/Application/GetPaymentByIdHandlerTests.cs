using B2B.Payment.Application.Interfaces;
using B2B.Payment.Application.Queries.GetPaymentById;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Tests.Application;

public sealed class GetPaymentByIdHandlerTests
{
    private readonly IReadPaymentRepository _repo = Substitute.For<IReadPaymentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetPaymentByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetPaymentByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetPaymentByIdHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PaymentEntity?)null);

        var result = await _handler.Handle(new GetPaymentByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var p = PaymentEntity.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", PaymentMethod.CreditCard);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var result = await _handler.Handle(new GetPaymentByIdQuery(p.Id), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Found_ShouldReturnDto()
    {
        var p = PaymentEntity.Create(Guid.NewGuid(), Guid.NewGuid(), TenantId, 75m, "USD", PaymentMethod.CreditCard);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var result = await _handler.Handle(new GetPaymentByIdQuery(p.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(p.Id);
        result.Value.Amount.Should().Be(75m);
        result.Value.Method.Should().Be("CreditCard");
        result.Value.Status.Should().Be("Pending");
    }
}
