using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
            IAccountService accountService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,            
            ILogger<AccountsController> logger)
    {
        _accountService = accountService;
        _userManager = userManager;             
        _context = context;
        _logger = logger;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
    }

    [HttpPost("register-admin")]
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)] 
    public async Task<IActionResult> RegisterAdmin([FromBody] AdminRegisterDto registerDto)
    {
        var (result, userId) = await _accountService.RegisterAdminAsync(registerDto);

        if (result.Succeeded && userId != null)
        {
            _logger.LogInformation("Admin registered successfully with ID: {UserId}", userId);
            
            return Ok(new { Message = "Admin registration successful.", UserId = userId });
        }
              
        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }

    [HttpPost("register-customer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRegisterDto registerDto)
    {
        var (result, userId) = await _accountService.RegisterCustomerAsync(registerDto);

        if (result.Succeeded && userId != null)
        {
            _logger.LogInformation("Customer registered successfully with ID: {UserId}", userId);
            return Ok(new { Message = "Customer registration successful.", UserId = userId });
        }

        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)] 
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        var result = await _accountService.LoginAsync(loginDto); 

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in.", loginDto.Email);
            var user = await _userManager.FindByEmailAsync(loginDto.Email); 

            if (user == null) 
            {
                await _accountService.LogoutAsync(); 
                _logger.LogError("Login succeeded for {Email} but user could not be found post-login.", loginDto.Email);
                return Problem(detail: "Login succeeded but failed to retrieve user details.", statusCode: StatusCodes.Status500InternalServerError, title:"Internal Error");
            }

            var roles = await _userManager.GetRolesAsync(user);
            string? tenantSubdomain = null; 
            if (user.TenantId != null)
            {
                var tenant = await _context.Tenants 
                        .AsNoTracking() 
                        .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);

                tenantSubdomain = tenant?.Subdomain; 
                if(tenant == null) {
                    _logger.LogWarning("Admin user {UserId} logged in but associated Tenant {TenantId} not found.", user.Id, user.TenantId.Value);
                }
            }

            var responseDto = new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email!,
                Name = user.Name,
                Roles = roles,
                AdministeredTenantId = user.TenantId,
                AdministeredTenantSubdomain = tenantSubdomain 
            };

            return Ok(responseDto); 
        }
        
        if (result.IsLockedOut) 
            return Problem(detail: "Account locked...", statusCode: StatusCodes.Status401Unauthorized, title: "Account Locked");
        if (result.IsNotAllowed) 
            return Problem(detail: "Login not allowed...", statusCode: StatusCodes.Status401Unauthorized, title: "Login Not Allowed"); 
        
        return Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Login Failed");
    }

    [HttpPost("logout")]
    [Authorize] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    public async Task<IActionResult> Logout()
    {
        
        var userName = User.Identity?.Name ?? "Unknown";
        await _accountService.LogoutAsync();
        _logger.LogInformation("User {UserName} logged out.", userName);
        
        return Ok(new { Message = "Logout successful." });
        
    }
}
