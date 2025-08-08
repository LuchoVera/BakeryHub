namespace BakeryHub.Application.Interfaces.BackgroundServices;

public interface IModelRetrainingService
{
    Task RetrainAllTenantModelsAsync();
}
