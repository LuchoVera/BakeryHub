using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
    }

    public async Task DeleteAsync(Guid categoryId, Guid tenantId)
    {
        var category = await GetByIdAndTenantAsync(categoryId, tenantId);
        if (category != null)
        {
            
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == categoryId && p.TenantId == tenantId);
            if (hasProducts)
            {
                 throw new InvalidOperationException("Cannot delete category with associated products.");
                 
            }
            _context.Categories.Remove(category);
        }
        
    }

     public async Task<bool> ExistsAsync(Guid categoryId, Guid tenantId)
    {
        return await _context.Categories.AnyAsync(c => c.Id == categoryId && c.TenantId == tenantId);
    }

    public async Task<IEnumerable<Category>> GetAllByTenantAsync(Guid tenantId)
    {
        return await _context.Categories
                       .Where(c => c.TenantId == tenantId)
                       .AsNoTracking()
                       .ToListAsync();
    }

    public async Task<Category?> GetByIdAndTenantAsync(Guid categoryId, Guid tenantId)
    {
         return await _context.Categories
                       .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == tenantId);
    }

    public async Task<bool> NameExistsForTenantAsync(string name, Guid tenantId)
    {
         
         return await _context.Categories
                        .AnyAsync(c => c.TenantId == tenantId && c.Name.ToLower() == name.ToLower());
    }

    public void Update(Category category)
    {
         _context.Entry(category).State = EntityState.Modified;
    }
}
