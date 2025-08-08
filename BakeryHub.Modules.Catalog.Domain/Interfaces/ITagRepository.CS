using BakeryHub.Modules.Catalog.Domain.Models;

namespace BakeryHub.Modules.Catalog.Domain.Interfaces;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid tagId, Guid tenantId);
    Task<Tag?> GetByNameAsync(string name, Guid tenantId);
    Task<List<Tag>> GetByNamesAsync(IEnumerable<string> names, Guid tenantId);
    Task AddAsync(Tag tag);
    void Update(Tag tag);
    Task<bool> DeleteAsync(Guid tagId, Guid tenantId);
    Task<IEnumerable<Tag>> GetAllByTenantAsync(Guid tenantId);
    Task<Tag> GetOrCreateTagAsync(string tagName, Guid tenantId);
}
