using BakeryHub.Application.Dtos;

namespace BakeryHub.Application.Interfaces;
public interface IRecommendationService
{
    Task<IEnumerable<ProductDto>> GetRecommendationsAsync(Guid userId, Guid tenantId, int count);
    Task<bool> RetrainTenantModelAsync(Guid tenantId);
}
