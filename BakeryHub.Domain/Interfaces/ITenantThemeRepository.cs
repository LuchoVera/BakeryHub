using BakeryHub.Domain.Entities;

namespace BakeryHub.Domain.Interfaces;
public interface ITenantThemeRepository
{
    Task<TenantTheme?> GetByTenantIdAsync(Guid tenantId, bool trackEntity = false);
    Task AddAsync(TenantTheme theme);
    void Update(TenantTheme theme);
}
