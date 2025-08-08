
using BakeryHub.Modules.Accounts.Application.Dtos.Admin;
using BakeryHub.Modules.Accounts.Application.Dtos.Auth;
using BakeryHub.Modules.Accounts.Application.Dtos.Customer;
using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Accounts.Application.Interfaces;

public interface IAccountService
{
    Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(AdminRegisterDto dto);
    Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerAsync(CustomerRegisterDto dto);
    Task<SignInResult> LoginAsync(LoginDto dto);
    Task LogoutAsync();
    Task<DetailedRegistrationResult> RegisterCustomerForTenantAsync(CustomerRegisterDto dto, Guid tenantId);
    Task<AuthResponseDto> LoginAndBuildResponseAsync(LoginDto loginDto);
    Task<AuthUserDto?> GetCurrentUserAsync(ApplicationUser user, string? subdomainContext);
    Task<EmailCheckResultDto> CheckEmailAsync(string email);
    Task<LinkAccountResult> LinkExistingCustomerToTenantAsync(string email, Guid tenantId);
    Task<IdentityResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    Task<(IdentityResult Result, AuthUserDto? UpdatedUser)> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto, string? subdomainContext);
    Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDto dto);
    Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto);
    Task<(IdentityResult Result, AuthUserDto? UpdatedUser)> UpdateAdminProfileAsync(Guid adminUserId, UpdateAdminProfileDto dto);
}
