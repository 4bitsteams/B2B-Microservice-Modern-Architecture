using B2B.Web.Models.Common;
using B2B.Web.Models.Products;

namespace B2B.Web.Services.Products;

public interface IProductService
{
    Task<PagedResult<ProductDto>?> GetProductsAsync(
        int page = 1, int pageSize = 20,
        string? search = null, Guid? categoryId = null);

    Task<ProductDto?> GetProductAsync(Guid id);
    Task<List<CategoryDto>?> GetCategoriesAsync();
    Task<ProductDto?> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request);
    Task<bool> AdjustStockAsync(Guid id, AdjustStockRequest request);
    Task<bool> ArchiveProductAsync(Guid id);
}
