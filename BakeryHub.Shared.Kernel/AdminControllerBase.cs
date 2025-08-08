
using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BakeryHub.Shared.Kernel;
public abstract class AdminControllerBase : ControllerBase
{
    protected readonly UserManager<ApplicationUser> _userManager;

    protected AdminControllerBase(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
    protected Guid GetCurrentAdminUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdString, out Guid userId))
        {
            return userId;
        }
        throw new InvalidOperationException("Cannot determine current Admin User ID from claims.");
    }

    protected async Task<Guid?> GetCurrentAdminTenantIdAsync()
    {
        var userId = GetCurrentAdminUserId();
        var adminUser = await _userManager.FindByIdAsync(userId.ToString());
        return adminUser?.TenantId;
    }
}
