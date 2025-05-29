using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BakeryHub.Infrastructure.Persistence.Repositories;

public class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid tagId, Guid tenantId)
    {
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.TenantId == tenantId);
    }

    public async Task<Tag?> GetByNameAsync(string name, Guid tenantId)
    {
        var normalizedName = name.Trim();
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && EF.Functions.ILike(t.Name, normalizedName));
    }

    public async Task<List<Tag>> GetByNamesAsync(IEnumerable<string> names, Guid tenantId)
    {
        var normalizedNames = names.Select(n => n.Trim()).ToList();
        return await _context.Tags
            .Where(t => t.TenantId == tenantId && normalizedNames.Any(nn => EF.Functions.ILike(t.Name, nn)))
            .ToListAsync();
    }

    public async Task AddAsync(Tag tag)
    {
        tag.Name = tag.Name.Trim();
        await _context.Tags.AddAsync(tag);
    }

    public void Update(Tag tag)
    {
        tag.Name = tag.Name.Trim();
        _context.Entry(tag).State = EntityState.Modified;
        tag.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<bool> DeleteAsync(Guid tagId, Guid tenantId)
    {
        var tag = await _context.Tags
            .Include(t => t.ProductTags)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.TenantId == tenantId);

        if (tag == null) return false;

        if (tag.ProductTags.Any())
        {
            _context.ProductTags.RemoveRange(tag.ProductTags);
        }

        _context.Tags.Remove(tag);

        return true;
    }

    public async Task<IEnumerable<Tag>> GetAllByTenantAsync(Guid tenantId)
    {
        return await _context.Tags
            .Where(t => t.TenantId == tenantId)
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Tag> GetOrCreateTagAsync(string tagName, Guid tenantId)
    {
        var normalizedTagName = tagName.Trim();

        var existingTag = await _context.Tags
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && EF.Functions.ILike(t.Name, normalizedTagName));

        if (existingTag != null)
        {
            return existingTag;
        }
        else
        {
            var newTag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = normalizedTagName,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _context.Tags.AddAsync(newTag);

            return newTag;
        }
    }
}
