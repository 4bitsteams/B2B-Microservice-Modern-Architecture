namespace B2B.Web.Models.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    string CategoryName,
    Guid CategoryId,
    bool IsActive,
    bool IsLowStock,
    DateTime CreatedAt);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int ProductCount);

public sealed record CreateProductRequest(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    Guid CategoryId);

public sealed record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    Guid CategoryId);

public sealed record AdjustStockRequest(int Quantity, string Reason);
