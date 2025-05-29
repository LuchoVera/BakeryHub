
using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BakeryHub.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;
    private readonly ApplicationDbContext _context; 

    public TagService(ITagRepository tagRepository, ApplicationDbContext context)
    {
        _tagRepository = tagRepository;
        _context = context;
    }

    private TagDto MapTagToDto(Tag tag) => new TagDto { Id = tag.Id, Name = tag.Name };

    public async Task<IEnumerable<TagDto>> GetAllTagsForAdminAsync(Guid adminTenantId)
    {
        var tags = await _tagRepository.GetAllByTenantAsync(adminTenantId);
        return tags.Select(MapTagToDto);
    }

    public async Task<TagDto?> GetTagByIdForAdminAsync(Guid tagId, Guid adminTenantId)
    {
        var tag = await _tagRepository.GetByIdAsync(tagId, adminTenantId);
        return tag == null ? null : MapTagToDto(tag);
    }

    public async Task<TagDto?> CreateTagForAdminAsync(CreateTagDto tagDto, Guid adminTenantId)
    {
        var trimmedName = tagDto.Name.Trim();
        
        var existingTagByName = await _tagRepository.GetByNameAsync(trimmedName, adminTenantId);

        if (existingTagByName != null)
        { 
            return null; 
        }
        
        var newTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            TenantId = adminTenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _tagRepository.AddAsync(newTag);
        await _context.SaveChangesAsync(); 
        return MapTagToDto(newTag);
    }

    public async Task<TagDto?> UpdateTagForAdminAsync(Guid tagId, UpdateTagDto tagDto, Guid adminTenantId)
    {
        var tagToUpdate = await _tagRepository.GetByIdAsync(tagId, adminTenantId);
        if (tagToUpdate == null)
        {
            return null; 
        }

        var trimmedNewName = tagDto.Name.Trim();
        if (!tagToUpdate.Name.Equals(trimmedNewName, StringComparison.OrdinalIgnoreCase))
        {
            var existingTagWithNewName = await _tagRepository.GetByNameAsync(trimmedNewName, adminTenantId);
            if (existingTagWithNewName != null && existingTagWithNewName.Id != tagId)
            {
                return null; 
            }
        }

        tagToUpdate.Name = trimmedNewName;
        _tagRepository.Update(tagToUpdate);
        await _context.SaveChangesAsync();

        return MapTagToDto(tagToUpdate);
    }

    public async Task<bool> DeleteTagForAdminAsync(Guid tagId, Guid adminTenantId)
    {
        var tag = await _tagRepository.GetByIdAsync(tagId, adminTenantId);
        if (tag == null)
        {
            return false; 
        }

        var success = await _tagRepository.DeleteAsync(tagId, adminTenantId);
        if (success)
        {
            await _context.SaveChangesAsync(); 
            return true;
        }
        return false;
    }

    public async Task<IEnumerable<TagDto>> GetPublicTagsForTenantAsync(Guid tenantId)
    {
        var tags = await _tagRepository.GetAllByTenantAsync(tenantId);
        return tags.Select(MapTagToDto);
    }
}
