using BakeryHub.Modules.Catalog.Application.Dtos;

namespace BakeryHub.Modules.Recommendations.Application.Interfaces;
public interface IRecommendationService
{
    Task<IEnumerable<ProductDto>> GetRecommendationsAsync(Guid userId, Guid tenantId, int count);
    Task<bool> RetrainTenantModelAsync(Guid tenantId);
    Task<IEnumerable<CategoryDto>> GetPreferredCategoriesForCustomerAsync(Guid tenantId, Guid userId);
}
