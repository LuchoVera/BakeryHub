using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Catalog.Application.Dtos;
using BakeryHub.Modules.Catalog.Application.Interfaces;
using BakeryHub.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Modules.Catalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TagsController : AdminControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _tagService = tagService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TagDto>>> GetTags()
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var tags = await _tagService.GetAllTagsForAdminAsync(adminTenantId.Value);
        return Ok(tags);
    }

    [HttpGet("{id:guid}", Name = "GetTagById")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagDto>> GetTagById(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var tag = await _tagService.GetTagByIdForAdminAsync(id, adminTenantId.Value);
        if (tag == null) return NotFound($"Tag with ID {id} not found for your tenant.");
        return Ok(tag);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TagDto>> CreateTag([FromBody] CreateTagDto tagDto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var createdTag = await _tagService.CreateTagForAdminAsync(tagDto, adminTenantId.Value);

        if (createdTag == null)
        {
            ModelState.AddModelError("Name", "A tag with this name already exists for your business.");
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetTagById), new { id = createdTag.Id }, createdTag);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateTagDto tagDto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var updatedTag = await _tagService.UpdateTagForAdminAsync(id, tagDto, adminTenantId.Value);

        if (updatedTag == null)
        {
            var originalTag = await _tagService.GetTagByIdForAdminAsync(id, adminTenantId.Value);
            if (originalTag == null)
            {
                return NotFound("Tag not found.");
            }
            else
            {
                ModelState.AddModelError("Name", "Another tag with this name already exists for your business.");
                return ValidationProblem(ModelState);
            }
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var tagExists = await _tagService.GetTagByIdForAdminAsync(id, adminTenantId.Value);
        if (tagExists == null)
        {
            return NotFound("Tag not found.");
        }

        var success = await _tagService.DeleteTagForAdminAsync(id, adminTenantId.Value);

        if (!success)
        {
            return BadRequest("Tag cannot be deleted because it is currently in use by one or more active products.");
        }

        return NoContent();
    }
}
