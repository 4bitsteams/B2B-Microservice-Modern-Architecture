using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Basket.Application.Commands.AddToBasket;
using B2B.Basket.Application.Commands.CheckoutBasket;
using B2B.Basket.Application.Commands.ClearBasket;
using B2B.Basket.Application.Commands.RemoveFromBasket;
using B2B.Basket.Application.Commands.UpdateBasketItem;
using B2B.Basket.Application.Queries.GetBasket;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Basket.Api.Controllers;

[Authorize]
[Route("api/basket")]
public sealed class BasketsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBasket(CancellationToken ct) =>
        (await sender.Send(new GetBasketQuery(), ct)).ToActionResult();

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddToBasketCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid productId, [FromBody] UpdateBasketItemRequest body, CancellationToken ct) =>
        (await sender.Send(new UpdateBasketItemCommand(productId, body.Quantity), ct)).ToActionResult();

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken ct) =>
        (await sender.Send(new RemoveFromBasketCommand(productId), ct)).ToActionResult();

    [HttpDelete]
    public async Task<IActionResult> ClearBasket(CancellationToken ct) =>
        (await sender.Send(new ClearBasketCommand(), ct)).ToActionResult();

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutBasketCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();
}

public sealed record UpdateBasketItemRequest(int Quantity);
