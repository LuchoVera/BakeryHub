using BakeryHub.Modules.Catalog.Application.Dtos;

namespace BakeryHub.Modules.Catalog.Application.Interfaces;

public interface ITagService
{
    Task<IEnumerable<TagDto>> GetAllTagsForAdminAsync(Guid adminTenantId);
    Task<TagDto?> GetTagByIdForAdminAsync(Guid tagId, Guid adminTenantId);
    Task<TagDto?> CreateTagForAdminAsync(CreateTagDto tagDto, Guid adminTenantId);
    Task<TagDto?> UpdateTagForAdminAsync(Guid tagId, UpdateTagDto tagDto, Guid adminTenantId);
    Task<bool> DeleteTagForAdminAsync(Guid tagId, Guid adminTenantId);
    Task<IEnumerable<TagDto>> GetPublicTagsForTenantAsync(Guid tenantId);
}
