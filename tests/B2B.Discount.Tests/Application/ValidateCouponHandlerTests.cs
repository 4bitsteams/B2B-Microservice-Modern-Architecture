using System.Linq.Expressions;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Application.Queries.ValidateCoupon;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Discount.Tests.Application;

public sealed class ValidateCouponHandlerTests
{
    private readonly IReadCouponRepository _repo = Substitute.For<IReadCouponRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ValidateCouponHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ValidateCouponHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new ValidateCouponHandler(_repo, _currentUser);
    }

    private void SetupFind(Coupon? coupon)
    {
        IReadOnlyList<Coupon> result = coupon is null ? Array.Empty<Coupon>() : new[] { coupon };
        _repo.FindAsync(Arg.Any<Expression<Func<Coupon, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnInvalid()
    {
        SetupFind(null);

        var result = await _handler.Handle(new ValidateCouponQuery("UNKNOWN", 100m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.InvalidReason.Should().Be("Coupon not found.");
    }

    [Fact]
    public async Task Handle_Expired_ShouldReturnInvalid()
    {
        var coupon = Coupon.Create("EXP", "X", DiscountType.Percentage, 10m, TenantId,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        SetupFind(coupon);

        var result = await _handler.Handle(new ValidateCouponQuery("EXP", 100m), default);

        result.Value.IsValid.Should().BeFalse();
        result.Value.InvalidReason.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_BelowMinimum_ShouldReturnInvalid()
    {
        var coupon = Coupon.Create("MIN", "X", DiscountType.Percentage, 10m, TenantId,
            minOrderAmount: 100m);
        SetupFind(coupon);

        var result = await _handler.Handle(new ValidateCouponQuery("MIN", 50m), default);

        result.Value.IsValid.Should().BeFalse();
        result.Value.InvalidReason.Should().Contain("Minimum");
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnValidWithCouponDetails()
    {
        var coupon = Coupon.Create("OK", "Save", DiscountType.Percentage, 15m, TenantId);
        SetupFind(coupon);

        var result = await _handler.Handle(new ValidateCouponQuery("OK", 100m), default);

        result.Value.IsValid.Should().BeTrue();
        result.Value.Code.Should().Be("OK");
        result.Value.DiscountValue.Should().Be(15m);
    }
}
