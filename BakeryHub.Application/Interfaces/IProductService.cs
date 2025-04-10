using BakeryHub.Application.Dtos;

namespace BakeryHub.Application.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAvailableProductsForAdminAsync(Guid adminTenantId);
    Task<IEnumerable<ProductDto>> GetAvailableProductsByCategoryForAdminAsync(Guid categoryId, Guid adminTenantId);
    Task<ProductDto?> GetProductByIdForAdminAsync(Guid productId, Guid adminTenantId);
    Task<ProductDto?> CreateProductForAdminAsync(CreateProductDto productDto, Guid adminTenantId);
    Task<bool> UpdateProductForAdminAsync(Guid productId, UpdateProductDto productDto, Guid adminTenantId);
    Task<bool> SetProductAvailabilityForAdminAsync(Guid productId, bool isAvailable, Guid adminTenantId);

       
}
