using BakeryHub.Application.Interfaces.BackgroundServices;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Recommendations.Application.Interfaces;

namespace BakeryHub.Modules.Recommendations.Application.Services;

public class ModelRetrainingService : IModelRetrainingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IRecommendationService _recommendationService;

    public ModelRetrainingService(
        ITenantRepository tenantRepository,
        IRecommendationService recommendationService)
    {
        _tenantRepository = tenantRepository;
        _recommendationService = recommendationService;
    }

    public async Task RetrainAllTenantModelsAsync()
    {
        var allTenants = await _tenantRepository.GetAllAsync();
        if (!allTenants.Any()) return;

        foreach (var tenant in allTenants)
        {
            try
            {
                await _recommendationService.RetrainTenantModelAsync(tenant.Id);
            }
            catch (Exception)
            {
                Console.WriteLine($"Error retraining model for tenant {tenant.Id}: {tenant.Name}");
            }
        }
    }
}
