using BakeryHub.Modules.Catalog.Application.Dtos;

namespace BakeryHub.Modules.Catalog.Application.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllProductsForAdminAsync(Guid adminTenantId, List<string>? tagNames = null);
    Task<IEnumerable<ProductDto>> GetAvailableProductsByCategoryForAdminAsync(Guid categoryId, Guid adminTenantId);
    Task<ProductDto?> GetProductByIdForAdminAsync(Guid productId, Guid adminTenantId);
    Task<ProductDto?> CreateProductForAdminAsync(CreateProductDto productDto, Guid adminTenantId);
    Task<bool> UpdateProductForAdminAsync(Guid productId, UpdateProductDto productDto, Guid adminTenantId);
    Task<bool> SetProductAvailabilityForAdminAsync(Guid productId, bool isAvailable, Guid adminTenantId);
    Task<bool> DeleteProductForAdminAsync(Guid productId, Guid adminTenantId);
    Task<IEnumerable<ProductDto>> GetPublicProductsByTenantIdAsync(
            Guid tenantId,
            string? searchTerm = null,
            Guid? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            List<string>? tagNames = null);
    Task<ProductDto?> GetPublicProductByIdAsync(Guid productId, Guid tenantId);
    Task<IEnumerable<ProductDto>> SearchPublicProductsByNameOrTagsAsync(
            Guid tenantId,
            string? searchTerm = null,
            List<string>? specificTagNames = null,
            Guid? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null);
}
