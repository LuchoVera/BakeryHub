using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using System.Globalization;

namespace BakeryHub.Application.Services;
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ApplicationDbContext _context;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ApplicationDbContext context)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _context = context;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsForAdminAsync(Guid adminTenantId)
    {
        var products = await _productRepository.GetAllProductsByTenantIdAsync(adminTenantId);
        var dtos = new List<ProductDto>();
        foreach (var p in products) { dtos.Add(await MapProductToDtoAsync(p)); }
        return dtos;
    }

    public async Task<IEnumerable<ProductDto>> GetAvailableProductsByCategoryForAdminAsync(Guid categoryId, Guid adminTenantId)
    {
        var products = await _productRepository.GetAvailableProductsByCategoryAndTenantGuidAsync(categoryId, adminTenantId);
        var dtos = new List<ProductDto>();
        foreach (var p in products) { dtos.Add(await MapProductToDtoAsync(p)); }
        return dtos;
    }

    public async Task<ProductDto?> GetProductByIdForAdminAsync(Guid productId, Guid adminTenantId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null || product.TenantId != adminTenantId) return null;
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
            TenantId = adminTenantId
        };

        await _productRepository.AddAsync(product);
        await _context.SaveChangesAsync();
        return await MapProductToDtoAsync(product);
    }

    public async Task<bool> UpdateProductForAdminAsync(Guid productId, UpdateProductDto productDto, Guid adminTenantId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null || product.TenantId != adminTenantId) return false;

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

        _productRepository.Update(product);
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
        var category = await _categoryRepository.GetByIdAndTenantAsync(product.CategoryId, product.TenantId);
        if (category != null) categoryName = category.Name;
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsAvailable = product.IsAvailable,
            Images = product.Images,
            LeadTimeDisplay = product.LeadTime ?? "N/A",
            CategoryId = product.CategoryId,
            CategoryName = categoryName
        };
    }
    public async Task<IEnumerable<ProductDto>> GetPublicProductsByTenantIdAsync(
            Guid tenantId,
            string? searchTerm = null,
            Guid? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null
            )
    {
        var allTenantProducts = await _productRepository.GetAllProductsByTenantIdAsync(
             tenantId,
             searchTerm,
             categoryId,
             minPrice,
             maxPrice
        );

        var availableProducts = allTenantProducts.Where(p => p.IsAvailable);
        var dtos = new List<ProductDto>();
        foreach (var product in availableProducts)
        {
            dtos.Add(await MapProductToDtoAsync(product));
        }
        return dtos;
    }

    public async Task<bool> DeleteProductForAdminAsync(Guid productId, Guid adminTenantId)
    {
        var product = await _productRepository.GetByIdAsync(productId);

        if (product == null || product.TenantId != adminTenantId) return false;

        if (product.IsAvailable) return false;

        await _productRepository.DeleteAsync(productId);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProductDto?> GetPublicProductByIdAsync(Guid productId, Guid tenantId)
    {
        var product = await _productRepository.GetByIdAsync(productId);

        if (product == null || product.TenantId != tenantId || !product.IsAvailable)
            return null;

        return await MapProductToDtoAsync(product);
    }

    public async Task<IEnumerable<ProductDto>> SearchPublicProductsByNameAsync(
        Guid tenantId,
        string searchTerm,
        Guid? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null)
    {
        var searchedProducts = await _productRepository.SearchPublicProductsByNameAsync(
            tenantId, searchTerm, categoryId, minPrice, maxPrice);

        var availableProducts = searchedProducts.Where(p => p.IsAvailable);

        var dtos = new List<ProductDto>();
        foreach (var product in availableProducts)
        {
            dtos.Add(await MapProductToDtoAsync(product));
        }
        return dtos;
    }
}
