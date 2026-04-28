using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Shared.Core.Common;
using B2B.Order.Application.Commands.CancelOrder;
using B2B.Order.Application.Commands.ConfirmOrder;
using B2B.Order.Application.Commands.CreateOrder;
using B2B.Order.Application.Commands.DeliverOrder;
using B2B.Order.Application.Commands.ShipOrder;
using B2B.Order.Application.Queries.GetOrderById;
using B2B.Order.Application.Queries.GetOrders;
using B2B.Order.Domain.Entities;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Order.Api.Controllers;

[Route("api/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController(ISender sender) : ApiControllerBase
{
    // ── Queries ────────────────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(PagedList<OrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetOrdersQuery(page, pageSize, status), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var withKey = command with { IdempotencyKey = idempotencyKey ?? string.Empty };
        var result = await sender.Send(withKey, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.OrderId }, result.Value)
            : Problem(result.Error);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmOrderCommand(id), ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    [HttpPost("{id:guid}/ship")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ship(Guid id, [FromBody] ShipOrderCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { OrderId = id }, ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeliverOrderCommand(id), ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { OrderId = id }, ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}
