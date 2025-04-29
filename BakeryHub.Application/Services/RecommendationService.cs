using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Application.Recommendations;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BakeryHub.Application.Services;
public class RecommendationService : IRecommendationService
{
    private readonly MLContext _mlContext;
    private readonly IProductRepository _productRepository;
    private readonly IConfiguration _configuration;
    private readonly string _modelPath;
    private readonly string _mockPurchasesPath;
    private readonly string _mockDetailsPath;
    private ITransformer? _trainedModel;
    private PredictionEngine<ProductRating, ProductRatingPrediction>? _predictionEngine;
    private DataMappings? _dataMappings;

    public RecommendationService(
        MLContext mlContext,
        IProductRepository productRepository,
        IConfiguration configuration)
    {
        _mlContext = mlContext;
        _productRepository = productRepository;
        _configuration = configuration;
        _mockPurchasesPath = _configuration["RecommendationSettings:PurchasesPath"] ?? "Resources/mock_compras.csv";
        _mockDetailsPath = _configuration["RecommendationSettings:DetailsPath"] ?? "Resources/mock_detalles_compras.csv";
        _modelPath = _configuration["RecommendationSettings:ModelPath"] ?? "recommendation_model.zip";
    }

    private bool EnsureModelLoaded()
    {
        if (_trainedModel != null && _predictionEngine != null && _dataMappings != null)
        {
            return true;
        }

        try
        {
            if (_dataMappings == null)
            {
                var dataLoader = new DataLoader(_mlContext);
                _dataMappings = dataLoader.LoadMappingsAndHistory(_mockPurchasesPath, _mockDetailsPath);
                if (_dataMappings == null || !_dataMappings.ProductGuidToIntMap.Any())
                {
                    return false;
                }
            }

            if (!File.Exists(_modelPath))
            {
#if DEBUG
                bool trainingSuccess = TrainAndSaveModel();
                if (!trainingSuccess)
                {
                    return false;
                }
#else
                    return false;
#endif
            }
            else
            {
                if (_trainedModel == null)
                {
                    DataViewSchema modelSchema;
                    _trainedModel = _mlContext.Model.Load(_modelPath, out modelSchema);
                }
            }

            if (_trainedModel != null && _predictionEngine == null)
            {
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRating, ProductRatingPrediction>(_trainedModel);
            }

            return _trainedModel != null && _predictionEngine != null && _dataMappings != null;
        }
        catch (Exception)
        {
            _trainedModel = null;
            _predictionEngine = null;
            _dataMappings = null;
            return false;
        }
    }

    private bool TrainAndSaveModel()
    {
        try
        {
            var dataLoader = new DataLoader(_mlContext);
            _dataMappings = dataLoader.LoadMappingsAndHistory(_mockPurchasesPath, _mockDetailsPath);
            if (_dataMappings == null) throw new InvalidOperationException("Failed to load mappings for training.");

            IDataView trainData = dataLoader.LoadData(_mockPurchasesPath, _mockDetailsPath, _dataMappings);
            if (trainData == null || trainData.Preview().RowView.Length == 0) throw new InvalidOperationException("No data loaded for training.");

            var modelTrainer = new ModelTrainer(_mlContext);
            _trainedModel = modelTrainer.TrainModel(trainData);
            _mlContext.Model.Save(_trainedModel, trainData.Schema, _modelPath);
            return true;
        }
        catch (Exception)
        {
            _trainedModel = null;
            return false;
        }
    }

    public async Task<IEnumerable<ProductDto>> GetRecommendationsAsync(Guid userId, Guid tenantId, int count)
    {
        if (!EnsureModelLoaded() || _predictionEngine == null || _dataMappings == null)
        {
            return Enumerable.Empty<ProductDto>();
        }

        if (!_dataMappings.UserGuidToFloatMap.TryGetValue(userId, out float userFloatId)) { return Enumerable.Empty<ProductDto>(); }
        HashSet<Guid> purchasedProductGuids = _dataMappings.UserPurchaseHistory.TryGetValue(userId, out var purchased) ? purchased : new HashSet<Guid>();
        var allTenantProducts = await _productRepository.GetAllProductsByTenantIdAsync(tenantId);
        if (!allTenantProducts.Any()) { return Enumerable.Empty<ProductDto>(); }

        var predictions = new List<(Guid ProductGuid, float Score)>();
        foreach (var product in allTenantProducts)
        {
            if (purchasedProductGuids.Contains(product.Id)) continue;
            if (!_dataMappings.ProductGuidToIntMap.TryGetValue(product.Id, out int productIntId)) continue;
            if (!_dataMappings.ProductIntToCategoryIntMap.TryGetValue(productIntId, out int categoryIntId)) { categoryIntId = 0; }

            var predictionInput = new ProductRating { UserId = userFloatId, ProductId = productIntId, CategoryId = categoryIntId };
            var prediction = _predictionEngine.Predict(predictionInput);
            predictions.Add((product.Id, prediction.Score));
        }
        var topProductGuids = predictions.OrderByDescending(p => p.Score).Take(count).Select(p => p.ProductGuid).ToList();
        var recommendedProducts = topProductGuids
                                .Select(guid => allTenantProducts.FirstOrDefault(p => p.Id == guid))
                                .Where(p => p != null)
                                .Select(p => MapProductToDto(p!))
                                .ToList();

        return recommendedProducts;
    }

    private ProductDto MapProductToDto(Product product)
    {
        if (product == null)
        {
            throw new ArgumentNullException(nameof(product), "Cannot map a null product.");
        }

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
}
