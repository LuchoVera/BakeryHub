using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BakeryHub.Application.Services;
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository; 
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository, 
        ApplicationDbContext context,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _context = context;
        _logger = logger;
    }


    public async Task<IEnumerable<ProductDto>> GetAvailableProductsForAdminAsync(Guid adminTenantId)
    {
        var products = await _productRepository.GetAvailableProductsByTenantGuidAsync(adminTenantId);
        var dtos = new List<ProductDto>();
        foreach(var p in products) { dtos.Add(await MapProductToDtoAsync(p)); } 
        return dtos;
    }

    public async Task<IEnumerable<ProductDto>> GetAvailableProductsByCategoryForAdminAsync(Guid categoryId, Guid adminTenantId)
    {
            var products = await _productRepository.GetAvailableProductsByCategoryAndTenantGuidAsync(categoryId, adminTenantId);
            var dtos = new List<ProductDto>();
        foreach(var p in products) { dtos.Add(await MapProductToDtoAsync(p)); }
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
            _logger.LogWarning("CreateProduct: Invalid CategoryId {CatId} for Tenant {TenantId}", productDto.CategoryId, adminTenantId);
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
            LeadTime = ParseLeadTime(productDto.LeadTimeInput),
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
            _logger.LogWarning("UpdateProduct: Invalid CategoryId {CatId} for Tenant {TenantId}", productDto.CategoryId, adminTenantId);
            return false;
        }

        product.Name = productDto.Name;
        product.Description = productDto.Description;
        product.Price = productDto.Price;
        product.IsAvailable = productDto.IsAvailable;
        product.Images = productDto.Images ?? new List<string>();
        product.LeadTime = ParseLeadTime(productDto.LeadTimeInput);
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

        private async Task<ProductDto> MapProductToDtoAsync(Product product) {
        string categoryName = "Unknown";
        var category = await _categoryRepository.GetByIdAndTenantAsync(product.CategoryId, product.TenantId); 
        if (category != null) categoryName = category.Name;
        return new ProductDto {
                Id = product.Id, Name = product.Name, Description = product.Description,
                Price = product.Price, IsAvailable = product.IsAvailable, Images = product.Images,
                LeadTimeDisplay = FormatLeadTime(product.LeadTime), CategoryId = product.CategoryId, CategoryName = categoryName
            };
        }
        private string FormatLeadTime(TimeSpan leadTime) { 
            if (leadTime == TimeSpan.Zero) return string.Empty;
            if (Math.Abs(leadTime.TotalDays) >= 1) return $"{leadTime.TotalDays:0.#} day(s)";
            if (Math.Abs(leadTime.TotalHours) >= 1) return $"{leadTime.TotalHours:0.#} hour(s)";
            return $"{leadTime.TotalMinutes:0} minute(s)";
        }
        private TimeSpan ParseLeadTime(string? leadTimeInput) {
        if (string.IsNullOrWhiteSpace(leadTimeInput)) return TimeSpan.Zero;
        leadTimeInput = leadTimeInput.ToLowerInvariant().Trim();
        var parts = leadTimeInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double value)) return TimeSpan.Zero;
            try {
                return parts[1] switch {
                    "day" or "days" => TimeSpan.FromDays(value),
                    "hour" or "hours" => TimeSpan.FromHours(value),
                    "minute" or "minutes" => TimeSpan.FromMinutes(value),
                    _ => TimeSpan.Zero };
            } catch(OverflowException) { return TimeSpan.Zero; }
        }
}
