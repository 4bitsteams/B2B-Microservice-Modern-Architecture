using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Vendor.Application.Commands.ApproveVendor;
using B2B.Vendor.Application.Commands.DeactivateVendor;
using B2B.Vendor.Application.Commands.ReactivateVendor;
using B2B.Vendor.Application.Commands.RegisterVendor;
using B2B.Vendor.Application.Commands.SuspendVendor;
using B2B.Vendor.Application.Commands.UpdateVendorProfile;
using B2B.Vendor.Application.Queries.GetVendorById;
using B2B.Vendor.Application.Queries.GetVendors;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Vendor.Api.Controllers;

[Authorize]
[Route("api/vendors")]
public sealed class VendorsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetVendors([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new GetVendorsQuery(page, pageSize), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> RegisterVendor(RegisterVendorCommand command, CancellationToken ct) =>
        (await sender.Send(command, ct)).ToActionResult();

    [HttpPost("{vendorId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid vendorId, [FromBody] ApproveRequest body, CancellationToken ct) =>
        (await sender.Send(new ApproveVendorCommand(vendorId, body.CommissionRate), ct)).ToActionResult();

    [HttpPost("{vendorId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid vendorId, [FromBody] SuspendRequest body, CancellationToken ct) =>
        (await sender.Send(new SuspendVendorCommand(vendorId, body.Reason), ct)).ToActionResult();

    [HttpGet("{vendorId:guid}")]
    public async Task<IActionResult> GetById(Guid vendorId, CancellationToken ct) =>
        (await sender.Send(new GetVendorByIdQuery(vendorId), ct)).ToActionResult();

    [HttpPut("{vendorId:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid vendorId, [FromBody] UpdateVendorProfileBody body, CancellationToken ct) =>
        (await sender.Send(new UpdateVendorProfileCommand(vendorId, body.CompanyName, body.ContactEmail,
            body.ContactPhone, body.Address, body.City, body.Country, body.Website, body.Description), ct)).ToActionResult();

    [HttpPost("{vendorId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid vendorId, CancellationToken ct) =>
        (await sender.Send(new DeactivateVendorCommand(vendorId), ct)).ToActionResult();

    [HttpPost("{vendorId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid vendorId, CancellationToken ct) =>
        (await sender.Send(new ReactivateVendorCommand(vendorId), ct)).ToActionResult();
}

public sealed record ApproveRequest(decimal CommissionRate);
public sealed record SuspendRequest(string Reason);
public sealed record UpdateVendorProfileBody(
    string CompanyName,
    string ContactEmail,
    string? ContactPhone,
    string Address,
    string City,
    string Country,
    string? Website,
    string? Description);
