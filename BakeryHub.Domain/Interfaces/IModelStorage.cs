namespace BakeryHub.Domain.Interfaces;

public interface IModelStorage
{
    Task SaveModelAsync(Guid tenantId, Stream modelStream);
    Task<Stream?> LoadModelAsync(Guid tenantId);
    Task<bool> ModelExistsAsync(Guid tenantId);
    Task DeleteModelAsync(Guid tenantId);
}
