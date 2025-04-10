using BakeryHub.Domain.Entities;
namespace BakeryHub.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllByTenantAsync(Guid tenantId);
    Task<Category?> GetByIdAndTenantAsync(Guid categoryId, Guid tenantId);
    Task AddAsync(Category category);
    void Update(Category category);
    Task DeleteAsync(Guid categoryId, Guid tenantId);
    Task<bool> ExistsAsync(Guid categoryId, Guid tenantId);
    Task<bool> NameExistsForTenantAsync(string name, Guid tenantId);
}
