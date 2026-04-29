using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Discount.Application.Commands.ApplyCoupon;
using B2B.Discount.Application.Commands.CreateCoupon;
using B2B.Discount.Application.Queries.ValidateCoupon;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Discount.Api.Controllers;

[Authorize]
[Route("api/coupons")]
public sealed class CouponsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateCoupon(CreateCouponCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpGet("validate")]
    public async Task<IActionResult> ValidateCoupon([FromQuery] string code, [FromQuery] decimal orderAmount, CancellationToken ct) =>
        (await sender.Send(new ValidateCouponQuery(code, orderAmount), ct)).ToActionResult();

    [HttpPost("apply")]
    public async Task<IActionResult> ApplyCoupon(ApplyCouponCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();
}
