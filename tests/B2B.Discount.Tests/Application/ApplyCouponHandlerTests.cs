using B2B.Discount.Application.Commands.ApplyCoupon;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Discount.Tests.Application;

public sealed class ApplyCouponHandlerTests
{
    private readonly ICouponRepository _repo = Substitute.For<ICouponRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ApplyCouponHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ApplyCouponHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ApplyCouponHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByCodeAsync("UNKNOWN", TenantId, Arg.Any<CancellationToken>()).Returns((Coupon?)null);

        var result = await _handler.Handle(new ApplyCouponCommand("UNKNOWN", 100m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Coupon.NotFound");
    }

    [Fact]
    public async Task Handle_NotAvailable_ShouldReturnValidation()
    {
        var coupon = Coupon.Create("EXP", "X", DiscountType.Percentage, 10m, TenantId,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        _repo.GetByCodeAsync("EXP", TenantId, Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await _handler.Handle(new ApplyCouponCommand("EXP", 100m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Coupon.NotAvailable");
    }

    [Fact]
    public async Task Handle_BelowMinimumOrder_ShouldReturnValidation()
    {
        var coupon = Coupon.Create("MIN", "X", DiscountType.Percentage, 10m, TenantId,
            minOrderAmount: 50m);
        _repo.GetByCodeAsync("MIN", TenantId, Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await _handler.Handle(new ApplyCouponCommand("MIN", 30m), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Coupon.InvalidApplication");
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnComputedSavings()
    {
        var coupon = Coupon.Create("SAVE10", "X", DiscountType.Percentage, 10m, TenantId);
        _repo.GetByCodeAsync("SAVE10", TenantId, Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await _handler.Handle(new ApplyCouponCommand("SAVE10", 100m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalAmount.Should().Be(100m);
        result.Value.DiscountedAmount.Should().Be(90m);
        result.Value.Savings.Should().Be(10m);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _repo.Received(1).Update(coupon);
    }
}
