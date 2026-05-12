using System.Net.Http.Json;
using B2B.Web.Models.Common;
using B2B.Web.Models.Products;

namespace B2B.Web.Services.Products;

public sealed class ProductService(IHttpClientFactory httpClientFactory) : IProductService
{
    private HttpClient Http => httpClientFactory.CreateClient("auth");

    public async Task<PagedResult<ProductDto>?> GetProductsAsync(
        int page = 1, int pageSize = 20,
        string? search = null, Guid? categoryId = null)
    {
        var query = $"/api/products?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (categoryId.HasValue)                query += $"&categoryId={categoryId}";

        return await Http.GetFromJsonAsync<PagedResult<ProductDto>>(query);
    }

    public Task<ProductDto?> GetProductAsync(Guid id)
        => Http.GetFromJsonAsync<ProductDto>($"/api/products/{id}");

    public Task<List<CategoryDto>?> GetCategoriesAsync()
        => Http.GetFromJsonAsync<List<CategoryDto>>("/api/categories");

    public async Task<ProductDto?> CreateProductAsync(CreateProductRequest request)
    {
        var response = await Http.PostAsJsonAsync("/api/products", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProductDto>()
            : null;
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var response = await Http.PutAsJsonAsync($"/api/products/{id}", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProductDto>()
            : null;
    }

    public async Task<bool> AdjustStockAsync(Guid id, AdjustStockRequest request)
    {
        var response = await Http.PatchAsJsonAsync($"/api/products/{id}/stock", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveProductAsync(Guid id)
    {
        var response = await Http.DeleteAsync($"/api/products/{id}");
        return response.IsSuccessStatusCode;
    }
}
