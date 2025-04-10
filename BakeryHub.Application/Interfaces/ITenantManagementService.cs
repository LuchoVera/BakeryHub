using BakeryHub.Application.Dtos;
namespace BakeryHub.Application.Interfaces;
public interface ITenantManagementService
{
    Task<TenantDto?> GetTenantForAdminAsync(Guid adminUserId);
    Task<bool> UpdateTenantForAdminAsync(Guid adminUserId, UpdateTenantDto tenantDto);
}
