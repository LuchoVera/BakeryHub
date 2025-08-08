using BakeryHub.Infrastructure.Persistence;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Catalog.Infrastructure.Persistence.Repositories;

public class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid tagId, Guid tenantId)
    {
        return await _context.Set<Tag>()
            .FirstOrDefaultAsync(t => t.Id == tagId && t.TenantId == tenantId);
    }

    public async Task<Tag?> GetByNameAsync(string name, Guid tenantId)
    {
        var normalizedName = name.Trim();
        return await _context.Set<Tag>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && EF.Functions.ILike(t.Name, normalizedName));
    }

    public async Task<List<Tag>> GetByNamesAsync(IEnumerable<string> names, Guid tenantId)
    {
        var normalizedNames = names.Select(n => n.Trim()).ToList();
        return await _context.Set<Tag>()
            .Where(t => t.TenantId == tenantId && normalizedNames.Any(nn => EF.Functions.ILike(t.Name, nn)))
            .ToListAsync();
    }

    public async Task AddAsync(Tag tag)
    {
        tag.Name = tag.Name.Trim();
        await _context.Set<Tag>().AddAsync(tag);
    }

    public void Update(Tag tag)
    {
        tag.Name = tag.Name.Trim();
        _context.Entry(tag).State = EntityState.Modified;
        tag.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<bool> DeleteAsync(Guid tagId, Guid tenantId)
    {
        var tag = await _context.Set<Tag>()
            .Include(t => t.ProductTags)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.TenantId == tenantId);

        if (tag == null) return false;

        if (tag.ProductTags.Any())
        {
            _context.Set<ProductTag>().RemoveRange(tag.ProductTags);
        }

        _context.Set<Tag>().Remove(tag);

        return true;
    }

    public async Task<IEnumerable<Tag>> GetAllByTenantAsync(Guid tenantId)
    {
        return await _context.Set<Tag>()
            .Where(t => t.TenantId == tenantId)
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Tag> GetOrCreateTagAsync(string tagName, Guid tenantId)
    {
        var normalizedTagName = tagName.Trim();

        var existingTag = await _context.Set<Tag>()
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
            await _context.Set<Tag>().AddAsync(newTag);

            return newTag;
        }
    }
}
