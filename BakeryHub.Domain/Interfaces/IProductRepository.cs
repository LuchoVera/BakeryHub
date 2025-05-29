using BakeryHub.Domain.Entities;

namespace BakeryHub.Domain.Interfaces;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAvailableProductsByTenantAsync(string tenantIdString);
    Task<IEnumerable<Product>> GetAllProductsByTenantIdAsync(
        Guid tenantId,
        string? searchTerm = null,
        Guid? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        List<string>? tagNames = null);
    Task<IEnumerable<Product>> GetAvailableProductsByCategoryAndTenantGuidAsync(Guid categoryId, Guid tenantId);
    Task<Product?> GetByIdAsync(Guid productId);
    Task AddAsync(Product product);
    void Update(Product product);
    Task DeleteAsync(Guid productId);
    Task<IEnumerable<Product>> SearchPublicProductsByNameOrTagsAsync(
        Guid tenantId,
        string? searchTerm = null,
        List<string>? tagNames = null,
        Guid? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null);
}
