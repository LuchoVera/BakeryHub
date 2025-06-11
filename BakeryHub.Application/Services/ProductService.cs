using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BakeryHub.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ApplicationDbContext _context;
    private readonly ITagRepository _tagRepository;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ApplicationDbContext context,
        ITagRepository tagRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _context = context;
        _tagRepository = tagRepository;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsForAdminAsync(Guid adminTenantId, List<string>? tagNames = null)
    {
        var products = await _productRepository.GetAllProductsByTenantIdAsync(
            adminTenantId,
            searchTerm: null,
            categoryId: null,
            minPrice: null,
            maxPrice: null,
            tagNames: tagNames);

        var dtos = new List<ProductDto>();
        foreach (var p in products) { dtos.Add(await MapProductToDtoAsync(p)); }
        return dtos;
    }

    public async Task<IEnumerable<ProductDto>> GetAvailableProductsByCategoryForAdminAsync(Guid categoryId, Guid adminTenantId)
    {

        var products = await _productRepository.GetAvailableProductsByCategoryAndTenantGuidAsync(categoryId, adminTenantId);
        var dtos = new List<ProductDto>();
        foreach (var p in products)
        {
            dtos.Add(await MapProductToDtoAsync(p));
        }
        return dtos;
    }

    public async Task<ProductDto?> GetProductByIdForAdminAsync(Guid productId, Guid adminTenantId)
    {

        var product = await _context.Products
                                .Include(p => p.Category)
                                .Include(p => p.ProductTags)
                                    .ThenInclude(pt => pt.Tag)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == adminTenantId);

        if (product == null) return null;
        return await MapProductToDtoAsync(product);
    }

    public async Task<ProductDto?> CreateProductForAdminAsync(CreateProductDto productDto, Guid adminTenantId)
    {
        if (!await _categoryRepository.ExistsAsync(productDto.CategoryId, adminTenantId))
        {
            return null;
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productDto.Name,
            Description = productDto.Description,
            Price = productDto.Price,
            IsAvailable = true,
            Images = productDto.Images ?? new List<string>(),
            LeadTime = productDto.LeadTimeInput,
            CategoryId = productDto.CategoryId,
            TenantId = adminTenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (productDto.Tags != null && productDto.Tags.Any())
        {
            product.ProductTags = new List<ProductTag>();
            foreach (var tagName in productDto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _tagRepository.GetOrCreateTagAsync(tagName, adminTenantId);
                product.ProductTags.Add(new ProductTag { Product = product, Tag = tag });
            }
        }

        await _productRepository.AddAsync(product);
        await _context.SaveChangesAsync();
        return await MapProductToDtoAsync(product);
    }

    public async Task<bool> UpdateProductForAdminAsync(Guid productId, UpdateProductDto productDto, Guid adminTenantId)
    {
        var product = await _context.Products
                                .Include(p => p.ProductTags)
                                    .ThenInclude(pt => pt.Tag)
                                .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == adminTenantId);

        if (product == null) return false;

        if (product.CategoryId != productDto.CategoryId &&
            !await _categoryRepository.ExistsAsync(productDto.CategoryId, adminTenantId))
        {
            return false;
        }

        product.Name = productDto.Name;
        product.Description = productDto.Description;
        product.Price = productDto.Price;
        product.Images = productDto.Images ?? new List<string>();
        product.LeadTime = productDto.LeadTimeInput;
        product.CategoryId = productDto.CategoryId;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        var newTagNames = productDto.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

        var productTagsToRemove = product.ProductTags
            .Where(pt => !newTagNames.Contains(pt.Tag.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var ptToRemove in productTagsToRemove)
        {
            _context.ProductTags.Remove(ptToRemove);
        }

        var currentTagNamesInProduct = product.ProductTags
            .Select(pt => pt.Tag.Name)
            .ToList();

        foreach (var tagName in newTagNames)
        {
            if (!currentTagNamesInProduct.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                var tagEntity = await _tagRepository.GetOrCreateTagAsync(tagName, adminTenantId);
                product.ProductTags.Add(new ProductTag { Product = product, Tag = tagEntity });
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetProductAvailabilityForAdminAsync(Guid productId, bool isAvailable, Guid adminTenantId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null || product.TenantId != adminTenantId) return false;

        product.IsAvailable = isAvailable;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        _productRepository.Update(product);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<ProductDto> MapProductToDtoAsync(Product product)
    {
        string categoryName = "Unknown";
        if (product.Category != null)
        {
            categoryName = product.Category.Name;
        }
        else
        {
            var category = await _categoryRepository.GetByIdAndTenantAsync(product.CategoryId, product.TenantId);
            if (category != null) categoryName = category.Name;
        }

        var tagNames = product.ProductTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsAvailable = product.IsAvailable,
            Images = product.Images ?? new List<string>(),
            LeadTimeDisplay = product.LeadTime ?? "N/A",
            CategoryId = product.CategoryId,
            CategoryName = categoryName,
            TagNames = tagNames
        };
    }
    public async Task<IEnumerable<ProductDto>> GetPublicProductsByTenantIdAsync(
           Guid tenantId,
           string? searchTerm = null,
           Guid? categoryId = null,
           decimal? minPrice = null,
           decimal? maxPrice = null,
           List<string>? tagNames = null)
    {
        var products = await _productRepository.GetAllProductsByTenantIdAsync(
             tenantId,
             searchTerm,
             categoryId,
             minPrice,
             maxPrice,
             tagNames);

        var availableProducts = products.Where(p => p.IsAvailable);
        var dtos = new List<ProductDto>();
        foreach (var product in availableProducts)
        {
            dtos.Add(await MapProductToDtoAsync(product));
        }
        return dtos;
    }

    public async Task<bool> DeleteProductForAdminAsync(Guid productId, Guid adminTenantId)
    {
        var productToSoftDelete = await _context.Products.IgnoreQueryFilters()
                                                        .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == adminTenantId);

        if (productToSoftDelete == null) return false;
        if (productToSoftDelete.IsDeleted) return true;

        productToSoftDelete.IsDeleted = true;
        productToSoftDelete.DeletedAt = DateTimeOffset.UtcNow;
        productToSoftDelete.IsAvailable = false;

        _context.Entry(productToSoftDelete).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProductDto?> GetPublicProductByIdAsync(Guid productId, Guid tenantId)
    {

        var product = await _context.Products
                                .Include(p => p.Category)
                                .Include(p => p.ProductTags)
                                    .ThenInclude(pt => pt.Tag)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId && p.IsAvailable);

        if (product == null) return null;
        return await MapProductToDtoAsync(product);
    }

    public async Task<IEnumerable<ProductDto>> SearchPublicProductsByNameOrTagsAsync(
       Guid tenantId,
       string? searchTerm = null,
       List<string>? specificTagNames = null,
       Guid? categoryId = null,
       decimal? minPrice = null,
       decimal? maxPrice = null)
    {
        var products = await _productRepository.SearchPublicProductsByNameOrTagsAsync(
            tenantId, searchTerm, specificTagNames, categoryId, minPrice, maxPrice);

        var dtos = new List<ProductDto>();
        foreach (var product in products)
        {
            dtos.Add(await MapProductToDtoAsync(product));
        }
        return dtos;
    }
}
