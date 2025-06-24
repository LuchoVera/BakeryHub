using BakeryHub.Application.Dtos;
using BakeryHub.Application.Dtos.Theme;
namespace BakeryHub.Application.Interfaces;

public interface ITenantManagementService
{
    Task<TenantDto?> GetTenantForAdminAsync(Guid adminUserId);
    Task<bool> UpdateTenantForAdminAsync(Guid adminUserId, UpdateTenantDto tenantDto);
    Task<TenantThemeDto> GetThemeForAdminAsync(Guid adminUserId);
    Task<bool> UpdateThemeForAdminAsync(Guid adminUserId, TenantThemeDto themeDto);
    Task<bool> ResetPublicThemeAsync(Guid adminUserId);
    Task<bool> ResetAdminThemeAsync(Guid adminUserId);
}
