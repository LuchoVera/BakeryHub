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

namespace BakeryHub.Application.Services
{
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
                return true; // Ya cargado para esta instancia/scope
            }

            Console.WriteLine($"RecommendationService (Instance {this.GetHashCode()}): Checking/Loading model and mappings...");

            try
            {
                // Cargar Mapeos siempre es necesario si no están cargados
                if (_dataMappings == null)
                {
                    var dataLoader = new DataLoader(_mlContext);
                    _dataMappings = dataLoader.LoadMappingsAndHistory(_mockPurchasesPath, _mockDetailsPath);
                    if (_dataMappings == null || !_dataMappings.ProductGuidToIntMap.Any())
                    {
                        Console.Error.WriteLine("RecommendationService ERROR: Failed to load mappings.");
                        return false;
                    }
                    Console.WriteLine("RecommendationService: Mappings loaded.");
                }


                // Verificar si el archivo del modelo existe
                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine($"RecommendationService INFO: Model file not found at {_modelPath}.");

                    // --- INICIO: Lógica para entrenar si no existe (en Desarrollo/Debug) ---
#if DEBUG // Esta directiva asegura que este bloque solo exista en compilaciones Debug
                    Console.WriteLine("Attempting to train and save model because file doesn't exist (DEBUG mode)...");
                    bool trainingSuccess = TrainAndSaveModel(); // Llama al método de entrenamiento

                    if (!trainingSuccess)
                    {
                        Console.Error.WriteLine("RecommendationService ERROR: Training failed. Cannot proceed.");
                        return false; // Falla si el entrenamiento no tuvo éxito
                    }
                    // Si el entrenamiento tuvo éxito, _trainedModel debería haberse asignado en TrainAndSaveModel.
                    // Procedemos a crear el prediction engine.

#else
                    // En modo Release (Producción), si el archivo no existe, es un error.
                    Console.Error.WriteLine($"RecommendationService ERROR: Model file not found at {_modelPath} in RELEASE mode.");
                    return false;
#endif
                    // --- FIN: Lógica para entrenar si no existe ---
                }
                else
                {
                    // Si el archivo SÍ existe, intentar cargarlo (si no está ya cargado)
                    if (_trainedModel == null)
                    {
                        Console.WriteLine($"RecommendationService: Loading existing model from {_modelPath}...");
                        DataViewSchema modelSchema;
                        _trainedModel = _mlContext.Model.Load(_modelPath, out modelSchema);
                        Console.WriteLine("RecommendationService: Existing model loaded.");
                    }
                }

                // Si llegamos aquí y _trainedModel no es nulo (ya sea cargado o recién entrenado),
                // crear el motor de predicción si no existe.
                if (_trainedModel != null && _predictionEngine == null)
                {
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRating, ProductRatingPrediction>(_trainedModel);
                    Console.WriteLine($"RecommendationService (Instance {this.GetHashCode()}): Prediction engine created/ready.");
                }

                // Devolver true solo si todo está listo
                return _trainedModel != null && _predictionEngine != null && _dataMappings != null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"RecommendationService ERROR during EnsureModelLoaded: {ex}");
                _trainedModel = null;
                _predictionEngine = null;
                _dataMappings = null;
                return false;
            }
        }

        private bool TrainAndSaveModel()
        {
            Console.WriteLine($"RecommendationService: Training and saving model to {_modelPath}...");
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
                Console.WriteLine("RecommendationService: Model trained and saved successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"RecommendationService ERROR during TrainAndSaveModel: {ex}");
                _trainedModel = null;
                return false;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetRecommendationsAsync(Guid userId, Guid tenantId, int count)
        {
            if (!EnsureModelLoaded() || _predictionEngine == null || _dataMappings == null)
            {
                Console.WriteLine($"RecommendationService WARNING: Model/Mappings not ready. Cannot generate recommendations for user {userId}.");
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
}
