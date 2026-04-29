using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Shipping.Application.Commands.CancelShipment;
using B2B.Shipping.Application.Commands.CreateShipment;
using B2B.Shipping.Application.Commands.DispatchShipment;
using B2B.Shipping.Application.Commands.MarkDelivered;
using B2B.Shipping.Application.Commands.UpdateTrackingInfo;
using B2B.Shipping.Application.Queries.GetShipmentByOrder;
using B2B.Shipping.Application.Queries.GetShipments;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Shipping.Api.Controllers;

[Authorize]
[Route("api/shipments")]
public sealed class ShipmentsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateShipment(CreateShipmentCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpGet("by-order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct) =>
        (await sender.Send(new GetShipmentByOrderQuery(orderId), ct)).ToActionResult();

    [HttpPost("{shipmentId:guid}/dispatch")]
    public async Task<IActionResult> Dispatch(Guid shipmentId, CancellationToken ct) =>
        (await sender.Send(new DispatchShipmentCommand(shipmentId), ct)).ToActionResult();

    [HttpPost("{shipmentId:guid}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid shipmentId, CancellationToken ct) =>
        (await sender.Send(new MarkDeliveredCommand(shipmentId), ct)).ToActionResult();

    [HttpGet]
    public async Task<IActionResult> GetShipments([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetShipmentsQuery(page, pageSize), ct)).ToActionResult();

    [HttpPost("{shipmentId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid shipmentId, CancellationToken ct) =>
        (await sender.Send(new CancelShipmentCommand(shipmentId), ct)).ToActionResult();

    [HttpPut("{shipmentId:guid}/tracking")]
    public async Task<IActionResult> UpdateTracking(Guid shipmentId, [FromBody] UpdateTrackingBody body, CancellationToken ct) =>
        (await sender.Send(new UpdateTrackingInfoCommand(shipmentId, body.NewTrackingNumber), ct)).ToActionResult();
}

public sealed record UpdateTrackingBody(string NewTrackingNumber);
