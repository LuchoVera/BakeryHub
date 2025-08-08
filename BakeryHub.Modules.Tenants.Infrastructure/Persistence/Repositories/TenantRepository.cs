using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Tenants.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;

    public TenantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(Guid tenantId)
    {
        return await _context.Tenants.FindAsync(tenantId);
    }

    public async Task<Tenant?> GetBySubdomainAsync(string subdomain)
    {
        var subdomainLower = subdomain?.ToLowerInvariant();
        return await _context.Tenants
                        .FirstOrDefaultAsync(t => t.Subdomain == subdomainLower);
    }

    public async Task<bool> SubdomainExistsAsync(string subdomain)
    {
        var subdomainLower = subdomain?.ToLowerInvariant();
        return await _context.Tenants.AnyAsync(t => t.Subdomain == subdomainLower);
    }

    public async Task AddAsync(Tenant tenant)
    {
        tenant.Subdomain = tenant.Subdomain?.ToLowerInvariant()!;
        await _context.Tenants.AddAsync(tenant);
    }

    public void Update(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync()
    {
        return await _context.Tenants.ToListAsync();
    }

    public async Task<Tenant?> GetBySubdomainWithThemeAsync(string subdomain)
    {
        var subdomainLower = subdomain?.ToLowerInvariant();
        return await _context.Tenants
                        .Include(t => t.Theme)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Subdomain == subdomainLower);
    }
}
