using BakeryHub.Application.Dtos.Theme;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Api.Controllers;

[ApiController]
[Route("api/admin/theme")]
[Authorize(Roles = "Admin")]
public class AdminThemeController : AdminControllerBase
{
    private readonly ITenantManagementService _tenantService;

    public AdminThemeController(ITenantManagementService tenantService, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantThemeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantThemeDto>> GetMyTheme()
    {
        var adminUserId = GetCurrentAdminUserId();
        var themeDto = await _tenantService.GetThemeForAdminAsync(adminUserId);
        return Ok(themeDto);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateMyTheme([FromBody] TenantThemeDto themeDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var adminUserId = GetCurrentAdminUserId();
        var success = await _tenantService.UpdateThemeForAdminAsync(adminUserId, themeDto);

        if (!success)
        {
            return BadRequest("Could not update theme. Ensure the administrator is associated with a tenant.");
        }

        return NoContent();
    }

    [HttpPost("reset-public")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPublicTheme()
    {
        var adminUserId = GetCurrentAdminUserId();
        var success = await _tenantService.ResetPublicThemeAsync(adminUserId);
        if (!success) return BadRequest("Could not reset public theme.");
        return NoContent();
    }

    [HttpPost("reset-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetAdminTheme()
    {
        var adminUserId = GetCurrentAdminUserId();
        var success = await _tenantService.ResetAdminThemeAsync(adminUserId);
        if (!success) return BadRequest("Could not reset admin theme.");
        return NoContent();
    }
}
