using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Application.Services;
public class TenantManagementService : ITenantManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public TenantManagementService(ITenantRepository tenantRepository, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _tenantRepository = tenantRepository;
        _userManager = userManager;
        _context = context;
    }

    public async Task<TenantDto?> GetTenantForAdminAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser == null || adminUser.TenantId == null)
        {
            return null;
        }
        var tenant = await _context.Tenants.FindAsync(adminUser.TenantId.Value);

        if (tenant == null)
        {
            return null;
        }

        return new TenantDto { Id = tenant.Id, Name = tenant.Name, Subdomain = tenant.Subdomain };
    }

    public async Task<bool> UpdateTenantForAdminAsync(Guid adminUserId, UpdateTenantDto tenantDto)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser == null || adminUser.TenantId == null)
        {
            return false;
        }

        var tenant = await _context.Tenants.FindAsync(adminUser.TenantId.Value);
        if (tenant == null)
        {
            return false;
        }
        tenant.Name = tenantDto.Name;

        await _context.SaveChangesAsync();
        return true;
    }
}
