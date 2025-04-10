using BakeryHub.Domain.Entities;

namespace BakeryHub.Domain.Interfaces;
public interface ITenantRepository
{
    Task<Tenant?> GetBySubdomainAsync(string subdomain);
    Task AddAsync(Tenant tenant);
    Task<bool> SubdomainExistsAsync(string subdomain);
    Task<IEnumerable<Tenant>> GetAllAsync();
}
