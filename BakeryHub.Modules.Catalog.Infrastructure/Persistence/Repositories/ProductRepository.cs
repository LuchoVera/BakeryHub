using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Catalog.Infrastructure.Persistence.Repositories;

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
        return await _context.Set<Product>()
                        .Where(p => p.TenantId == tenantGuid.Value && p.IsAvailable)
                        .Include(p => p.Category)
                        .AsNoTracking()
                        .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAllProductsByTenantIdAsync(
        Guid tenantId,
        string? searchTerm = null,
        Guid? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        List<string>? tagNames = null)
    {
        var query = _context.Set<Product>()
                        .Where(p => p.TenantId == tenantId)
                        .Include(p => p.Category)
                        .Include(p => p.ProductTags)
                            .ThenInclude(pt => pt.Tag)
                        .AsNoTracking();

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTermLower = searchTerm.ToLowerInvariant().Trim();
            query = query.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(searchTermLower)) ||
                (p.Description != null && p.Description.ToLower().Contains(searchTermLower))
            );
        }

        if (tagNames != null && tagNames.Any())
        {
            var lowerTagNames = tagNames.Select(tn => tn.ToLowerInvariant()).ToList();
            query = query.Where(p => p.ProductTags.Any(pt => lowerTagNames.Contains(pt.Tag.Name.ToLower())));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue && maxPrice.Value >= 0)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAvailableProductsByCategoryAndTenantGuidAsync(Guid categoryId, Guid tenantId)
    {
        return await _context.Set<Product>()
                    .Where(p => p.TenantId == tenantId && p.CategoryId == categoryId && p.IsAvailable)
                    .AsNoTracking()
                    .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid productId)
    {
        return await _context.Set<Product>().FindAsync(productId);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Set<Product>().AddAsync(product);
    }

    public void Update(Product product)
    {
        _context.Entry(product).State = EntityState.Modified;
    }

    public async Task DeleteAsync(Guid productId)
    {
        var product = await _context.Set<Product>().IgnoreQueryFilters()
                                .FirstOrDefaultAsync(p => p.Id == productId);
        if (product != null)
        {
            product.IsDeleted = true;
            product.DeletedAt = DateTimeOffset.UtcNow;
            product.IsAvailable = false;
            _context.Set<Product>().Update(product);
        }
    }

    public async Task<IEnumerable<Product>> SearchPublicProductsByNameOrTagsAsync(
       Guid tenantId,
       string? searchTerm = null,
       List<string>? tagNames = null,
       Guid? categoryId = null,
       decimal? minPrice = null,
       decimal? maxPrice = null)
    {
        var query = _context.Set<Product>()
            .Where(p => p.TenantId == tenantId && p.IsAvailable)
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTermLower = searchTerm.ToLowerInvariant().Trim();
            query = query.Where(p =>
                (p.Name != null && EF.Functions.ILike(p.Name, $"%{searchTermLower}%")) ||
                (p.Description != null && EF.Functions.ILike(p.Description, $"%{searchTermLower}%")) ||
                p.ProductTags.Any(pt => EF.Functions.ILike(pt.Tag.Name, $"%{searchTermLower}%"))
            );
        }

        if (tagNames != null && tagNames.Any())
        {
            var lowerTagNames = tagNames.Select(tn => tn.ToLowerInvariant()).ToList();
            foreach (var tagName in lowerTagNames)
            {
                query = query.Where(p => p.ProductTags.Any(pt => EF.Functions.ILike(pt.Tag.Name, tagName)));
            }
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue && maxPrice.Value >= 0)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Product?> GetByIdForAdminAsync(Guid productId, Guid tenantId)
    {
        return await _context.Set<Product>()
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId);
    }

    public async Task<Product?> GetByIdPublicAsync(Guid productId, Guid tenantId)
    {
        return await _context.Set<Product>()
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId && p.IsAvailable);
    }
}
