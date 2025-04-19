using BakeryHub.Domain.Entities;

namespace BakeryHub.Domain.Interfaces;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAvailableProductsByTenantAsync(string tenantIdString);
    Task<IEnumerable<Product>> GetAvailableProductsByTenantIdAsync(Guid tenantId);
    Task<IEnumerable<Product>> GetAvailableProductsByCategoryAndTenantGuidAsync(Guid categoryId, Guid tenantId);
    Task<Product?> GetByIdAsync(Guid productId);
    Task AddAsync(Product product);
    void Update(Product product);
}
