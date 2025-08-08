using BakeryHub.Domain.Entities;

namespace BakeryHub.Domain.Interfaces;
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid tenantId);
    Task<Tenant?> GetBySubdomainAsync(string subdomain);
    Task AddAsync(Tenant tenant);
    void Update(Tenant tenant);
    Task<bool> SubdomainExistsAsync(string subdomain);
    Task<IEnumerable<Tenant>> GetAllAsync();
    Task<Tenant?> GetBySubdomainWithThemeAsync(string subdomain);
}
