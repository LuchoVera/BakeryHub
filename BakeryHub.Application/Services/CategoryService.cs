using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;

namespace BakeryHub.Application.Services;
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ApplicationDbContext _context;

    public CategoryService(ICategoryRepository categoryRepository, ApplicationDbContext context)
    {
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
        if (await _categoryRepository.NameExistsForTenantAsync(categoryDto.Name, adminTenantId))
        {
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
        catch
        {
            var exists = await _categoryRepository.ExistsAsync(categoryId, adminTenantId);
            return !exists;
        }
    }
}
