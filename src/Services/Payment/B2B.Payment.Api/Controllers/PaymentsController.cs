using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Payment.Application.Commands.ProcessPayment;
using B2B.Payment.Application.Commands.RefundPayment;
using B2B.Payment.Application.Queries.GetPaymentById;
using B2B.Payment.Application.Queries.GetPaymentsByOrder;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Payment.Api.Controllers;

[Authorize]
[Route("api/payments")]
public sealed class PaymentsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ProcessPayment(ProcessPaymentCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetPayment(Guid paymentId, CancellationToken ct) =>
        (await sender.Send(new GetPaymentByIdQuery(paymentId), ct)).ToActionResult();

    [HttpPost("{paymentId:guid}/refund")]
    public async Task<IActionResult> RefundPayment(Guid paymentId, CancellationToken ct) =>
        (await sender.Send(new RefundPaymentCommand(paymentId), ct)).ToActionResult();

    [HttpGet("by-order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct) =>
        (await sender.Send(new GetPaymentsByOrderQuery(orderId), ct)).ToActionResult();
}
