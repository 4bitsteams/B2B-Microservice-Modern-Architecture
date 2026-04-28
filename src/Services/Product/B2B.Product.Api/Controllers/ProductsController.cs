using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2B.Shared.Core.Common;
using B2B.Product.Application.Commands.AdjustStock;
using B2B.Product.Application.Commands.ArchiveProduct;
using B2B.Product.Application.Commands.CreateProduct;
using B2B.Product.Application.Commands.UpdateProduct;
using B2B.Product.Application.Queries.GetLowStockProducts;
using B2B.Product.Application.Queries.GetProductById;
using B2B.Product.Application.Queries.GetProducts;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Product.Api.Controllers;

[Route("api/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetProductsQuery(page, pageSize, search, categoryId), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetProductByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("low-stock")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(CancellationToken ct)
    {
        var result = await sender.Send(new GetLowStockProductsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(typeof(CreateProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.ProductId }, result.Value)
            : Problem(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { ProductId = id }, ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    [HttpPatch("{id:guid}/stock")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustStock(Guid id, [FromBody] AdjustStockCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { ProductId = id }, ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ArchiveProductCommand(id), ct);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}
