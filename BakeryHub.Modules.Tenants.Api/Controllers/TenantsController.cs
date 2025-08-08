using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Tenants.Application.Dtos.Tenant;
using BakeryHub.Modules.Tenants.Application.Interfaces;
using BakeryHub.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace BakeryHub.Modules.Tenants.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TenantsController : AdminControllerBase
{
    private readonly ITenantManagementService _tenantService;

    public TenantsController(ITenantManagementService tenantService, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _tenantService = tenantService;
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetMyTenantDetails()
    {
        var adminUserId = GetCurrentAdminUserId();
        var tenantDto = await _tenantService.GetTenantForAdminAsync(adminUserId);

        if (tenantDto == null) return NotFound("Tenant details not found for the current administrator.");
        return Ok(tenantDto);
    }

    [HttpPut("mine")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyTenant([FromBody] UpdateTenantDto tenantDto)
    {
        var adminUserId = GetCurrentAdminUserId();
        var success = await _tenantService.UpdateTenantForAdminAsync(adminUserId, tenantDto);

        if (!success) return NotFound("Tenant details not found or update failed.");
        return NoContent();
    }
}
