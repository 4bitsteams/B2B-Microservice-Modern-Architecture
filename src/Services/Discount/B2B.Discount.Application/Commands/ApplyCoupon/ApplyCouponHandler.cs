using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Discount.Application.Commands.ApplyCoupon;

/// <summary>
/// Applies a coupon code to an order and returns the discounted amount.
///
/// Concurrency safety: a distributed lock is acquired per coupon-code+tenant before
/// the check-then-act sequence (read UsageCount → increment → save). Without this lock,
/// two concurrent requests could both pass the <c>UsageCount &lt; MaxUsageCount</c> guard
/// and both persist an increment, silently exceeding the redemption limit.
/// </summary>
public sealed class ApplyCouponHandler(
    ICouponRepository couponRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDistributedLock distributedLock)
    : ICommandHandler<ApplyCouponCommand, ApplyCouponResponse>
{
    // Tune these to your SLA: hold the lock for at most 10 s (crash safety net),
    // wait up to 5 s before giving up, and poll every 200 ms.
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LockWait   = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockRetry  = TimeSpan.FromMilliseconds(200);

    public async Task<Result<ApplyCouponResponse>> Handle(
        ApplyCouponCommand request, CancellationToken cancellationToken)
    {
        // Scope lock to (code, tenant) — different tenants share no coupon state.
        var lockResource = $"coupon:{request.CouponCode.ToUpperInvariant()}:tenant:{currentUser.TenantId}";

        await using var handle = await distributedLock.AcquireAsync(
            lockResource, LockExpiry, LockWait, LockRetry, cancellationToken);

        if (handle?.IsAcquired != true)
        {
            return Error.ServiceUnavailable(
                "Coupon.LockUnavailable",
                "The coupon is being redeemed by another request. Please try again shortly.");
        }

        var coupon = await couponRepository.GetByCodeAsync(
            request.CouponCode, currentUser.TenantId, cancellationToken);

        if (coupon is null)
            return Error.NotFound("Coupon.NotFound", $"Coupon '{request.CouponCode}' not found.");

        var applyResult = coupon.Apply(request.OrderAmount);
        if (applyResult.IsFailure)
            return applyResult.Error;

        couponRepository.Update(coupon);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyCouponResponse(
            coupon.Code,
            request.OrderAmount,
            applyResult.Value,
            Math.Round(request.OrderAmount - applyResult.Value, 2));
    }
}
