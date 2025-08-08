using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Tenants.Infrastructure.Persistence.Repositories;

public class TenantThemeRepository : ITenantThemeRepository
{
    private readonly ApplicationDbContext _context;

    public TenantThemeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TenantTheme?> GetByTenantIdAsync(Guid tenantId, bool trackEntity = false)
    {
        var query = _context.TenantThemes.AsQueryable();
        if (!trackEntity)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(t => t.TenantId == tenantId);
    }

    public async Task AddAsync(TenantTheme theme)
    {
        await _context.TenantThemes.AddAsync(theme);
    }

    public void Update(TenantTheme theme)
    {
        _context.TenantThemes.Update(theme);
    }
}
