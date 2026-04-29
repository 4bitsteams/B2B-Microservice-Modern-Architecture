using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Payment.Application.Commands.CancelInvoice;
using B2B.Payment.Application.Commands.CreateInvoice;
using B2B.Payment.Application.Commands.MarkInvoicePaid;
using B2B.Payment.Application.Queries.GetInvoiceById;
using B2B.Payment.Application.Queries.GetInvoicesByTenant;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Payment.Api.Controllers;

[Authorize]
[Route("api/invoices")]
public sealed class InvoicesController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInvoices([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetInvoicesByTenantQuery(page, pageSize), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpPost("{invoiceId:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid invoiceId, [FromBody] MarkPaidRequest body, CancellationToken ct) =>
        (await sender.Send(new MarkInvoicePaidCommand(invoiceId, body.PaymentReference), ct)).ToActionResult();

    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GetById(Guid invoiceId, CancellationToken ct) =>
        (await sender.Send(new GetInvoiceByIdQuery(invoiceId), ct)).ToActionResult();

    [HttpPost("{invoiceId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invoiceId, [FromBody] CancelInvoiceBody body, CancellationToken ct) =>
        (await sender.Send(new CancelInvoiceCommand(invoiceId, body.Reason), ct)).ToActionResult();
}

public sealed record MarkPaidRequest(string PaymentReference);
public sealed record CancelInvoiceBody(string Reason);
