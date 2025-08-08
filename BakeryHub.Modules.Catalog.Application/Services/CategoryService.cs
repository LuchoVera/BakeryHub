using BakeryHub.Modules.Catalog.Application.Dtos;
using BakeryHub.Modules.Catalog.Application.Interfaces;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Domain.Models;
using BakeryHub.Domain.Interfaces;

namespace BakeryHub.Modules.Catalog.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
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
                _categoryRepository.Update(existingCategory);
                await _unitOfWork.SaveChangesAsync();
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
        await _unitOfWork.SaveChangesAsync();
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
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCategoryForAdminAsync(Guid categoryId, Guid adminTenantId)
    {
        try
        {
            await _categoryRepository.DeleteAsync(categoryId, adminTenantId);
            await _unitOfWork.SaveChangesAsync();
            return true;
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

    public async Task<IEnumerable<CategoryDto>> GetPublicCategoriesForTenantAsync(Guid tenantId)
    {
        var categories = await _categoryRepository.GetAllByTenantAsync(tenantId);
        return categories.Select(MapCategoryToDto);
    }
}
