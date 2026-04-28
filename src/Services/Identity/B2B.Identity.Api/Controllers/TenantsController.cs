using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Identity.Application.Queries.GetTenantBySlug;
using B2B.Identity.Application.Queries.GetTenants;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Identity.Api.Controllers;

[Route("api/tenants")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public sealed class TenantsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantBySlugQuery(slug), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
