using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace BakeryHub.Application.Services;
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(ICategoryRepository categoryRepository, ApplicationDbContext context, ILogger<CategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _context = context;
        _logger = logger;
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
        if (await _categoryRepository.NameExistsForTenantAsync(categoryDto.Name, adminTenantId))
        {
            _logger.LogWarning("CreateCategory: Duplicate name '{Name}' for Tenant {TenantId}", categoryDto.Name, adminTenantId);
            return null; 
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = categoryDto.Name,
            TenantId = adminTenantId
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
                    _logger.LogWarning("UpdateCategory: Duplicate name '{Name}' for Tenant {TenantId}", categoryDto.Name, adminTenantId);
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
            return true;
        }
        catch (InvalidOperationException ex) 
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}: Cannot delete category with products.", categoryId);
            return false;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}", categoryId);
            var exists = await _categoryRepository.ExistsAsync(categoryId, adminTenantId);
            return !exists;
        }
    }
}
