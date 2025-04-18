using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BakeryHub.Domain.Entities;

namespace BakeryHub.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class CategoriesController : AdminControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var categories = await _categoryService.GetAllForAdminAsync(adminTenantId.Value);
        return Ok(categories);
    }

    [HttpGet("{id:guid}", Name = "GetCategoryById")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var category = await _categoryService.GetByIdForAdminAsync(id, adminTenantId.Value);
        if (category == null) return NotFound($"Category with ID {id} not found for your tenant.");
        return Ok(category);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryDto categoryDto)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var createdCategory = await _categoryService.CreateCategoryForAdminAsync(categoryDto, adminTenantId.Value);
        if (createdCategory == null) return BadRequest("Failed to create category (e.g., name might already exist).");

        return CreatedAtAction(nameof(GetCategoryById), new { id = createdCategory.Id }, createdCategory);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryDto categoryDto)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var success = await _categoryService.UpdateCategoryForAdminAsync(id, categoryDto, adminTenantId.Value);
        if (!success) return NotFound($"Category with ID {id} not found for your tenant or update failed.");
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var success = await _categoryService.DeleteCategoryForAdminAsync(id, adminTenantId.Value);
        if (!success) return NotFound($"Category with ID {id} not found or cannot be deleted (maybe it has products?).");
        return NoContent();
    }
}
