using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Discount.Application.Commands.CreateDiscount;
using B2B.Discount.Application.Commands.DeactivateDiscount;
using B2B.Discount.Application.Queries.GetDiscountById;
using B2B.Discount.Application.Queries.GetDiscounts;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Discount.Api.Controllers;

[Authorize]
[Route("api/discounts")]
public sealed class DiscountsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDiscounts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetDiscountsQuery(page, pageSize), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> CreateDiscount(CreateDiscountCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpGet("{discountId:guid}")]
    public async Task<IActionResult> GetById(Guid discountId, CancellationToken ct) =>
        (await sender.Send(new GetDiscountByIdQuery(discountId), ct)).ToActionResult();

    [HttpPost("{discountId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid discountId, CancellationToken ct) =>
        (await sender.Send(new DeactivateDiscountCommand(discountId), ct)).ToActionResult();
}
