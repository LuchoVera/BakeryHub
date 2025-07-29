using Azure.Storage.Blobs;
using BakeryHub.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BakeryHub.Infrastructure.Storage;

public class AzureBlobModelStorage : IModelStorage
{
    private readonly BlobContainerClient _blobContainerClient;

    public AzureBlobModelStorage(IConfiguration configuration)
    {
        var storageConnectionString = configuration.GetConnectionString("BlobStorage");
        var blobServiceClient = new BlobServiceClient(storageConnectionString);
        _blobContainerClient = blobServiceClient.GetBlobContainerClient("tenant-models");
        _blobContainerClient.CreateIfNotExists();
    }

    private BlobClient GetBlobClient(Guid tenantId) => _blobContainerClient.GetBlobClient($"model_tenant_{tenantId}.zip");

    public async Task<bool> ModelExistsAsync(Guid tenantId) => await GetBlobClient(tenantId).ExistsAsync();

    public async Task<Stream?> LoadModelAsync(Guid tenantId)
    {
        var blobClient = GetBlobClient(tenantId);
        if (!await blobClient.ExistsAsync()) return null;

        var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        stream.Position = 0;
        return stream;
    }

    public async Task SaveModelAsync(Guid tenantId, Stream modelStream)
    {
        var blobClient = GetBlobClient(tenantId);
        modelStream.Position = 0;
        await blobClient.UploadAsync(modelStream, overwrite: true);
    }

    public async Task DeleteModelAsync(Guid tenantId) => await GetBlobClient(tenantId).DeleteIfExistsAsync();
}
