using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Infrastructure.Persistence.Repositories;
public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantRepository _tenantRepository;

    public ProductRepository(ApplicationDbContext context, ITenantRepository tenantRepository)
    {
        _context = context;
        _tenantRepository = tenantRepository;
    }

    private async Task<Guid?> GetTenantGuidAsync(string tenantIdString)
    {
        var tenant = await _tenantRepository.GetBySubdomainAsync(tenantIdString);
        return tenant?.Id;
    }

    public async Task<IEnumerable<Product>> GetAvailableProductsByTenantAsync(string tenantIdString)
    {
        var tenantGuid = await GetTenantGuidAsync(tenantIdString);
        if (tenantGuid == null)
        {
            return Enumerable.Empty<Product>();
        }
        return await _context.Products
                        .Where(p => p.TenantId == tenantGuid.Value && p.IsAvailable)
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAllProductsByTenantIdAsync(Guid tenantId)
    {
        return await _context.Products
                        .Where(p => p.TenantId == tenantId)
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAvailableProductsByCategoryAndTenantGuidAsync(Guid categoryId, Guid tenantId)
    {
        return await _context.Products
                    .Where(p => p.TenantId == tenantId && p.CategoryId == categoryId && p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid productId)
    {
        return await _context.Products.FindAsync(productId);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
    }

    public void Update(Product product)
    {
        _context.Entry(product).State = EntityState.Modified;
    }
    
    public async Task DeleteAsync(Guid productId)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product != null)
        {
            _context.Products.Remove(product);
        }
    }

    public async Task<IEnumerable<Product>> SearchPublicProductsByNameAsync(Guid tenantId, string searchTerm)
    {
        var searchTermLower = searchTerm.ToLowerInvariant().Trim();

        var query = _context.Products
                        .Where(p => p.TenantId == tenantId && !string.IsNullOrEmpty(p.Name));

        if (!string.IsNullOrWhiteSpace(searchTermLower))
        {
            query = query.Where(p => p.Name.ToLower().Contains(searchTermLower));
        }
        else
        {
            return Enumerable.Empty<Product>();
        }

        return await query.Include(p => p.Category)
                          .AsNoTracking()
                          .ToListAsync();
    }
}
