using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Application.Recommendations;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Collections.Concurrent;

namespace BakeryHub.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly MLContext _mlContext;
    private readonly IProductRepository _productRepository;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly string _modelBaseDirectory;

    private readonly ConcurrentDictionary<Guid, ITransformer> _tenantModels = new();
    private readonly ConcurrentDictionary<Guid, PredictionEngine<ProductRating, ProductRatingPrediction>> _tenantPredictionEngines = new();
    private readonly ConcurrentDictionary<Guid, DataMappings> _tenantDataMappings = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _tenantLocks = new();

    public RecommendationService(
        MLContext mlContext,
        IProductRepository productRepository,
        ApplicationDbContext dbContext,
        IConfiguration configuration)
    {
        _mlContext = mlContext;
        _productRepository = productRepository;
        _dbContext = dbContext;
        _configuration = configuration;

        var configuredModelPath = _configuration["RecommendationSettings:ModelPath"];
        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            throw new InvalidOperationException("RecommendationSettings:ModelPath is not configured in appsettings.json.");
        }

        _modelBaseDirectory = Path.IsPathRooted(configuredModelPath)
            ? configuredModelPath
            : Path.Combine(AppContext.BaseDirectory, configuredModelPath);

        if (!Directory.Exists(_modelBaseDirectory))
        {
            Directory.CreateDirectory(_modelBaseDirectory);
        }
    }

    private string GetModelPathForTenant(Guid tenantId)
    {
        return Path.Combine(_modelBaseDirectory, $"model_tenant_{tenantId}.zip");
    }

    private SemaphoreSlim GetTenantLock(Guid tenantId)
    {
        return _tenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<(PredictionEngine<ProductRating, ProductRatingPrediction>? Engine, DataMappings? Mappings)>
        EnsureModelAndMappingsLoadedForTenantAsync(Guid tenantId)
    {
        if (_tenantPredictionEngines.TryGetValue(tenantId, out var cachedEngine) &&
            _tenantDataMappings.TryGetValue(tenantId, out var cachedMappings))
        {
            if (cachedEngine == null && (cachedMappings == null || !cachedMappings.ProductGuidToIntMap.Any()))
            {
                return (null, cachedMappings);
            }
            if (cachedEngine != null && cachedMappings != null && cachedMappings.ProductGuidToIntMap.Any())
            {
                return (cachedEngine, cachedMappings);
            }
        }

        var tenantLock = GetTenantLock(tenantId);
        await tenantLock.WaitAsync();

        try
        {
            if (_tenantPredictionEngines.TryGetValue(tenantId, out cachedEngine) &&
                _tenantDataMappings.TryGetValue(tenantId, out cachedMappings))
            {
                if (cachedEngine == null && (cachedMappings == null || !cachedMappings.ProductGuidToIntMap.Any()))
                {
                    return (null, cachedMappings);
                }
                if (cachedEngine != null && cachedMappings != null && cachedMappings.ProductGuidToIntMap.Any())
                {
                    return (cachedEngine, cachedMappings);
                }
            }

            DataMappings? tenantDataMappings;
            ITransformer? tenantTrainedModel = null;
            PredictionEngine<ProductRating, ProductRatingPrediction>? predictionEngine = null;

            var modelPathForTenant = GetModelPathForTenant(tenantId);
            var dataLoader = new DataLoader(_mlContext, _dbContext);

            tenantDataMappings = await dataLoader.LoadMappingsAndHistoryForTenantAsync(tenantId);

            if (tenantDataMappings == null || !tenantDataMappings.ProductGuidToIntMap.Any() || !tenantDataMappings.UserGuidToFloatMap.Any())
            {
                _tenantDataMappings[tenantId] = tenantDataMappings ?? new DataMappings();
                _tenantModels.TryRemove(tenantId, out _);
                _tenantPredictionEngines.TryRemove(tenantId, out _);
                return (null, tenantDataMappings);
            }

            if (File.Exists(modelPathForTenant))
            {
                try
                {
                    DataViewSchema modelSchema;
                    tenantTrainedModel = _mlContext.Model.Load(modelPathForTenant, out modelSchema);
                }
                catch
                {
                    tenantTrainedModel = null;
                }
            }

            if (tenantTrainedModel == null)
            {
                IDataView? trainData = dataLoader.LoadDataForTenant(tenantDataMappings);

                if (trainData == null || trainData.GetColumn<float>(nameof(ProductRating.UserId)).Count() == 0)
                {
                    _tenantDataMappings[tenantId] = tenantDataMappings;
                    return (null, tenantDataMappings);
                }

                var modelTrainer = new ModelTrainer(_mlContext);
                try
                {
                    tenantTrainedModel = modelTrainer.TrainModel(trainData);
                    _mlContext.Model.Save(tenantTrainedModel, trainData.Schema, modelPathForTenant);
                }
                catch
                {
                    tenantTrainedModel = null;
                }
            }

            if (tenantTrainedModel != null)
            {
                predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRating, ProductRatingPrediction>(tenantTrainedModel);
                _tenantModels[tenantId] = tenantTrainedModel;
                _tenantPredictionEngines[tenantId] = predictionEngine;
            }

            _tenantDataMappings[tenantId] = tenantDataMappings;
            return (predictionEngine, tenantDataMappings);
        }
        finally
        {
            tenantLock.Release();
        }
    }

    public async Task<IEnumerable<ProductDto>> GetRecommendationsAsync(Guid userId, Guid tenantId, int count)
    {
        var (predictionEngine, dataMappings) = await EnsureModelAndMappingsLoadedForTenantAsync(tenantId);

        if (predictionEngine == null || dataMappings == null || !dataMappings.ProductGuidToIntMap.Any())
        {
            return Enumerable.Empty<ProductDto>();
        }

        if (!dataMappings.UserGuidToFloatMap.TryGetValue(userId, out float userFloatId))
        {
            return Enumerable.Empty<ProductDto>();
        }

        HashSet<Guid> purchasedProductGuids = dataMappings.UserPurchaseHistory.TryGetValue(userId, out var purchased)
            ? purchased
            : new HashSet<Guid>();

        var allTenantProductsFromDb = await _productRepository.GetAllProductsByTenantIdAsync(tenantId);
        if (!allTenantProductsFromDb.Any())
        {
            return Enumerable.Empty<ProductDto>();
        }

        var predictions = new List<(Guid ProductGuid, float Score)>();

        foreach (var productEntity in allTenantProductsFromDb)
        {
            if (purchasedProductGuids.Contains(productEntity.Id)) continue;
            if (!dataMappings.ProductGuidToIntMap.TryGetValue(productEntity.Id, out int productIntId)) continue;

            int categoryIntId = dataMappings.ProductIntToCategoryIntMap.TryGetValue(productIntId, out int catIntId) ? catIntId : 0;

            var predictionInput = new ProductRating { UserId = userFloatId, ProductId = productIntId, CategoryId = categoryIntId };
            var predictionResult = predictionEngine.Predict(predictionInput);
            predictions.Add((productEntity.Id, predictionResult.Score));
        }

        var topProductGuids = predictions
            .OrderByDescending(p => p.Score)
            .Take(count)
            .Select(p => p.ProductGuid)
            .ToList();

        var recommendedProductDtos = topProductGuids
            .Select(guid => allTenantProductsFromDb.FirstOrDefault(p => p.Id == guid))
            .Where(p => p != null && p.IsAvailable)
            .Select(p => MapProductToDto(p!))
            .ToList();

        return recommendedProductDtos;
    }

    private ProductDto MapProductToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsAvailable = product.IsAvailable,
            Images = product.Images ?? new List<string>(),
            LeadTimeDisplay = string.IsNullOrWhiteSpace(product.LeadTime) ? "N/A" : product.LeadTime,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? "Desconocida"
        };
    }

    public async Task<bool> RetrainTenantModelAsync(Guid tenantId)
    {
        var tenantLock = GetTenantLock(tenantId);
        await tenantLock.WaitAsync();

        try
        {
            var modelPathForTenant = GetModelPathForTenant(tenantId);
            var dataLoader = new DataLoader(_mlContext, _dbContext);
            DataMappings? tenantDataMappings = await dataLoader.LoadMappingsAndHistoryForTenantAsync(tenantId);

            if (tenantDataMappings == null || !tenantDataMappings.ProductGuidToIntMap.Any() || !tenantDataMappings.UserGuidToFloatMap.Any())
            {
                if (File.Exists(modelPathForTenant)) File.Delete(modelPathForTenant);
                _tenantModels.TryRemove(tenantId, out _);
                _tenantPredictionEngines.TryRemove(tenantId, out _);
                _tenantDataMappings.TryRemove(tenantId, out _);
                return false;
            }

            IDataView? trainData = dataLoader.LoadDataForTenant(tenantDataMappings);
            if (trainData == null || trainData.GetColumn<float>(nameof(ProductRating.UserId)).Count() == 0)
            {
                if (File.Exists(modelPathForTenant)) File.Delete(modelPathForTenant);
                _tenantModels.TryRemove(tenantId, out _);
                _tenantPredictionEngines.TryRemove(tenantId, out _);
                _tenantDataMappings[tenantId] = tenantDataMappings;
                return false;
            }

            var modelTrainer = new ModelTrainer(_mlContext);
            ITransformer? newTrainedModel;
            try
            {
                newTrainedModel = modelTrainer.TrainModel(trainData);
            }
            catch { return false; }

            if (newTrainedModel != null)
            {
                try
                {
                    _mlContext.Model.Save(newTrainedModel, trainData.Schema, modelPathForTenant);
                    var newPredictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRating, ProductRatingPrediction>(newTrainedModel);
                    _tenantModels[tenantId] = newTrainedModel;
                    _tenantPredictionEngines[tenantId] = newPredictionEngine;
                    _tenantDataMappings[tenantId] = tenantDataMappings;
                    return true;
                }
                catch { return false; }
            }
            return false;
        }
        finally
        {
            tenantLock.Release();
        }
    }
}
