using BakeryHub.Application.Dtos;

namespace BakeryHub.Application.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllForAdminAsync(Guid adminTenantId);
    Task<CategoryDto?> GetByIdForAdminAsync(Guid categoryId, Guid adminTenantId);
    Task<CategoryDto?> CreateCategoryForAdminAsync(CreateCategoryDto categoryDto, Guid adminTenantId);
    Task<bool> UpdateCategoryForAdminAsync(Guid categoryId, UpdateCategoryDto categoryDto, Guid adminTenantId);
    Task<bool> DeleteCategoryForAdminAsync(Guid categoryId, Guid adminTenantId);
    Task<IEnumerable<CategoryDto>> GetPreferredCategoriesForCustomerAsync(Guid tenantId, Guid userId);
}
