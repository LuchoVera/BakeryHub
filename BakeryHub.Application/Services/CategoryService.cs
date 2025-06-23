using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ApplicationDbContext _context;

    public CategoryService(ICategoryRepository categoryRepository, ApplicationDbContext context, IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
        _categoryRepository = categoryRepository;
        _context = context;
    }

    private CategoryDto MapCategoryToDto(Category category) => new CategoryDto { Id = category.Id, Name = category.Name };

    public async Task<IEnumerable<CategoryDto>> GetAllForAdminAsync(Guid adminTenantId)
    {
        var categories = await _categoryRepository.GetAllByTenantAsync(adminTenantId);
        return categories.Select(MapCategoryToDto);
    }

    public async Task<CategoryDto?> GetByIdForAdminAsync(Guid categoryId, Guid adminTenantId)
    {
        var category = await _categoryRepository.GetByIdAndTenantAsync(categoryId, adminTenantId);
        return category == null ? null : MapCategoryToDto(category);
    }

    public async Task<CategoryDto?> CreateCategoryForAdminAsync(CreateCategoryDto categoryDto, Guid adminTenantId)
    {
        var existingCategory = await _categoryRepository.GetByNameAndTenantIgnoreQueryFiltersAsync(categoryDto.Name, adminTenantId);

        if (existingCategory != null)
        {
            if (existingCategory.IsDeleted)
            {
                existingCategory.IsDeleted = false;
                existingCategory.DeletedAt = null;

                _context.Categories.Update(existingCategory);

                await _context.SaveChangesAsync();
                return MapCategoryToDto(existingCategory);
            }
            else
            {
                return null;
            }
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = categoryDto.Name,
            TenantId = adminTenantId,
            IsDeleted = false,
        };

        await _categoryRepository.AddAsync(category);
        await _context.SaveChangesAsync();
        return MapCategoryToDto(category);
    }

    public async Task<bool> UpdateCategoryForAdminAsync(Guid categoryId, UpdateCategoryDto categoryDto, Guid adminTenantId)
    {
        var category = await _categoryRepository.GetByIdAndTenantAsync(categoryId, adminTenantId);
        if (category == null) return false;

        if (!category.Name.Equals(categoryDto.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (await _categoryRepository.NameExistsForTenantAsync(categoryDto.Name, adminTenantId))
            {
                return false;
            }
        }

        category.Name = categoryDto.Name;
        _categoryRepository.Update(category);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCategoryForAdminAsync(Guid categoryId, Guid adminTenantId)
    {
        try
        {
            await _categoryRepository.DeleteAsync(categoryId, adminTenantId);
            await _context.SaveChangesAsync();

            var checkCategory = await _context.Categories
                                           .IgnoreQueryFilters()
                                           .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == adminTenantId);
            return checkCategory?.IsDeleted ?? false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<CategoryDto>> GetPreferredCategoriesForCustomerAsync(Guid tenantId, Guid userId)
    {
        var allCategoriesForTenant = await _categoryRepository.GetAllByTenantAsync(tenantId);
        if (!allCategoriesForTenant.Any())
        {
            return Enumerable.Empty<CategoryDto>();
        }

        var userOrders = await _orderRepository.GetOrdersWithItemsAndProductCategoriesAsync(userId, tenantId);

        if (!userOrders.Any())
        {
            return allCategoriesForTenant.OrderBy(c => c.Name).Select(MapCategoryToDto);
        }

        var categoryPurchaseCounts = new Dictionary<Guid, int>();

        foreach (var order in userOrders)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.Product?.Category != null)
                {
                    Guid categoryId = item.Product.CategoryId;
                    if (categoryPurchaseCounts.ContainsKey(categoryId))
                    {
                        categoryPurchaseCounts[categoryId] += item.Quantity;
                    }
                    else
                    {
                        categoryPurchaseCounts[categoryId] = item.Quantity;
                    }
                }
            }
        }

        var sortedCategories = allCategoriesForTenant
            .Select(c => new
            {
                Category = c,
                Score = categoryPurchaseCounts.GetValueOrDefault(c.Id, 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Category.Name)
            .Select(x => MapCategoryToDto(x.Category))
            .ToList();

        return sortedCategories;
    }
    public async Task<IEnumerable<CategoryDto>> GetPublicCategoriesForTenantAsync(Guid tenantId)
    {
        var categories = await _categoryRepository.GetAllByTenantAsync(tenantId);
        return categories.Select(MapCategoryToDto);
    }
}
