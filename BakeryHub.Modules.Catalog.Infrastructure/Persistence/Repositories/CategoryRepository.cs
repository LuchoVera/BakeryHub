using BakeryHub.Infrastructure.Persistence;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Catalog.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Category category)
    {
        await _context.Set<Category>().AddAsync(category);
    }

    public async Task DeleteAsync(Guid categoryId, Guid tenantId)
    {
        var category = await _context.Set<Category>()
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == tenantId);
        if (category != null)
        {
            if (category.IsDeleted) return;

            bool hasActiveAssociatedProducts = await _context.Set<Product>()
                                                      .AnyAsync(p => p.CategoryId == categoryId && p.TenantId == tenantId && !p.IsDeleted);

            if (hasActiveAssociatedProducts)
            {
                throw new InvalidOperationException();
            }
            category.IsDeleted = true;
            category.DeletedAt = DateTimeOffset.UtcNow;
            _context.Set<Category>().Update(category);
        }
    }

    public async Task<bool> ExistsAsync(Guid categoryId, Guid tenantId)
    {
        return await _context.Set<Category>().AnyAsync(c => c.Id == categoryId && c.TenantId == tenantId);
    }

    public async Task<IEnumerable<Category>> GetAllByTenantAsync(Guid tenantId)
    {
        return await _context.Set<Category>()
                       .Where(c => c.TenantId == tenantId)
                       .AsNoTracking()
                       .ToListAsync();
    }

    public async Task<Category?> GetByIdAndTenantAsync(Guid categoryId, Guid tenantId)
    {
        return await _context.Set<Category>()
                      .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == tenantId);
    }

    public async Task<bool> NameExistsForTenantAsync(string name, Guid tenantId)
    {
        return await _context.Set<Category>()
                        .AnyAsync(c => c.TenantId == tenantId && EF.Functions.ILike(c.Name, name));
    }

    public void Update(Category category)
    {
        _context.Entry(category).State = EntityState.Modified;
    }

    public async Task<Category?> GetByNameAndTenantIgnoreQueryFiltersAsync(string name, Guid tenantId)
    {
        return await _context.Set<Category>()
                             .IgnoreQueryFilters()
                             .FirstOrDefaultAsync(c => c.TenantId == tenantId && EF.Functions.ILike(c.Name, name));
    }
}
