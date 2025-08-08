using BakeryHub.Modules.Recommendations.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BakeryHub.Modules.Recommendations.Infrastructure.Storage;

public class LocalFileModelStorage : IModelStorage
{
    private readonly string _basePath;

    public LocalFileModelStorage(IConfiguration configuration)
    {
        _basePath = configuration.GetValue<string>("RecommendationSettings:ModelPath") ?? "TenantRecommendationModels";
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    private string GetModelPath(Guid tenantId) => Path.Combine(_basePath, $"model_tenant_{tenantId}.zip");

    public Task<bool> ModelExistsAsync(Guid tenantId) => Task.FromResult(File.Exists(GetModelPath(tenantId)));

    public async Task<Stream?> LoadModelAsync(Guid tenantId)
    {
        var modelPath = GetModelPath(tenantId);
        if (!File.Exists(modelPath)) return null;

        var memoryStream = new MemoryStream();
        using (var fileStream = new FileStream(modelPath, FileMode.Open, FileAccess.Read))
        {
            await fileStream.CopyToAsync(memoryStream);
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task SaveModelAsync(Guid tenantId, Stream modelStream)
    {
        var modelPath = GetModelPath(tenantId);
        modelStream.Position = 0;
        using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write))
        {
            await modelStream.CopyToAsync(fileStream);
        }
    }

    public Task DeleteModelAsync(Guid tenantId)
    {
        var modelPath = GetModelPath(tenantId);
        if (File.Exists(modelPath))
        {
            File.Delete(modelPath);
        }
        return Task.CompletedTask;
    }
}
