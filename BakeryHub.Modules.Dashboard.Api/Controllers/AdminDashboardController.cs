using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;
using BakeryHub.Modules.Dashboard.Application.Interfaces;
using BakeryHub.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Modules.Dashboard.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : AdminControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(
        IDashboardService dashboardService,
        UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("order-statistics")]
    [ProducesResponseType(typeof(DashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DashboardResponseDto>> GetOrderDashboardStatistics(
        [FromQuery] DashboardQueryParametersDto queryParams)
    {
        var tenantId = await GetCurrentAdminTenantIdAsync();
        if (!tenantId.HasValue)
        {
            return Forbid("Admin not associated with a tenant.");
        }

        if (queryParams.TimePeriod?.ToLowerInvariant() == "customrange")
        {
            if (!queryParams.CustomStartDate.HasValue)
                ModelState.AddModelError(nameof(queryParams.CustomStartDate), "CustomStartDate is required for customrange time period.");
            if (!queryParams.CustomEndDate.HasValue)
                ModelState.AddModelError(nameof(queryParams.CustomEndDate), "CustomEndDate is required for customrange time period.");
            if (queryParams.CustomStartDate.HasValue && queryParams.CustomEndDate.HasValue && queryParams.CustomStartDate.Value > queryParams.CustomEndDate.Value)
                ModelState.AddModelError(nameof(queryParams.CustomStartDate), "CustomStartDate cannot be after CustomEndDate.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(ModelState));
        }

        try
        {
            var dashboardData = await _dashboardService.GetDashboardStatisticsAsync(tenantId.Value, queryParams);

            if (dashboardData == null)
            {
                return NotFound(new { message = "Could not retrieve order statistics." });
            }

            return Ok(dashboardData);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching order dashboard data.");
        }
    }
}
