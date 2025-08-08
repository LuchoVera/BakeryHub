using BakeryHub.Modules.Accounts.Application.Dtos.Admin;
using BakeryHub.Modules.Accounts.Application.Dtos.Auth;
using BakeryHub.Modules.Accounts.Application.Dtos.Customer;
using BakeryHub.Modules.Accounts.Application.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BakeryHub.Modules.Accounts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountsController(
            IAccountService accountService,
            UserManager<ApplicationUser> userManager)
    {
        _accountService = accountService;
        _userManager = userManager;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdString, out Guid userId))
        {
            return userId;
        }
        throw new InvalidOperationException("User ID is not valid.");
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
    public async Task<ActionResult<AuthUserDto>> GetCurrentUser([FromQuery] string? subdomain)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var userInfo = await _accountService.GetCurrentUserAsync(user, subdomain);
        if (userInfo == null)
        {
            return Unauthorized();
        }
        return Ok(userInfo);
    }

    [HttpGet("check-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EmailCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailCheckResultDto>> CheckEmailExists([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return BadRequest(new { message = "Valid email is required." });
        }
        var result = await _accountService.CheckEmailAsync(email);
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        var result = await _accountService.ChangePasswordAsync(userId, changePasswordDto);

        if (result.Succeeded)
        {
            return NoContent();
        }

        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }

    [HttpPut("me/update-profile")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateUserProfileDto updateUserProfileDto, [FromQuery] string? subdomain)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        var (result, updatedUser) = await _accountService.UpdateUserProfileAsync(userId, updateUserProfileDto, subdomain);

        if (result.Succeeded && updatedUser != null)
        {
            return Ok(updatedUser);
        }

        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _accountService.ForgotPasswordAsync(forgotPasswordDto);

        return Ok(new { message = "A password reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.ResetPasswordAsync(resetPasswordDto);

        if (result.Succeeded)
        {
            return Ok(new { message = "Your password has been reset successfully." });
        }

        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }

    [HttpPut("me/admin-profile")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateAdminProfile([FromBody] UpdateAdminProfileDto updateAdminProfileDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        var (result, updatedUser) = await _accountService.UpdateAdminProfileAsync(userId, updateAdminProfileDto);

        if (result.Succeeded && updatedUser != null)
        {
            return Ok(updatedUser);
        }

        AddIdentityErrors(result);
        return BadRequest(ModelState);
    }
}
