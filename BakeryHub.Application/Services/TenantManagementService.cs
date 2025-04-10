using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;


namespace BakeryHub.Application.Services;
public class TenantManagementService : ITenantManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(ITenantRepository tenantRepository, UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger<TenantManagementService> logger)
    {
        _tenantRepository = tenantRepository;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public async Task<TenantDto?> GetTenantForAdminAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser == null || adminUser.TenantId == null)
        {
            _logger.LogWarning("Attempt to get tenant details for non-admin or non-existent user {UserId}", adminUserId);
            return null;
        }
        var tenant = await _context.Tenants.FindAsync(adminUser.TenantId.Value);

        if (tenant == null)
        {
            _logger.LogError("Admin user {UserId} has TenantId {TenantId} but Tenant was not found in DB!", adminUserId, adminUser.TenantId.Value);
            return null;
        }

        return new TenantDto { Id = tenant.Id, Name = tenant.Name, Subdomain = tenant.Subdomain };
    }

    public async Task<bool> UpdateTenantForAdminAsync(Guid adminUserId, UpdateTenantDto tenantDto)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser == null || adminUser.TenantId == null)
        {
            _logger.LogWarning("Attempt to update tenant details for non-admin or non-existent user {UserId}", adminUserId);
            return false;
        }

        var tenant = await _context.Tenants.FindAsync(adminUser.TenantId.Value);
        if (tenant == null)
        {
            _logger.LogError("Admin user {UserId} has TenantId {TenantId} but Tenant was not found in DB during update!", adminUserId, adminUser.TenantId.Value);
            return false;
        }
        tenant.Name = tenantDto.Name;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Tenant {TenantId} updated successfully by Admin {AdminUserId}", tenant.Id, adminUserId);
        return true;
    }
}
