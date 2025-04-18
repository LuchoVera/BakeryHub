using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AccountsController(
            IAccountService accountService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
    {
        _accountService = accountService;
        _userManager = userManager;
        _context = context;
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var responseDto = await _accountService.LoginAndBuildResponseAsync(loginDto);

        if (responseDto != null)
        {
            return Ok(responseDto);
        }

        return Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Login Failed");
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await _accountService.LogoutAsync();
        return Ok(new { Message = "Logout successful." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserDto>> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var userInfo = await _accountService.GetCurrentUserAsync(user);
        return Ok(userInfo);
    }
}
