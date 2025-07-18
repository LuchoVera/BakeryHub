using BakeryHub.Application.Dtos;
using BakeryHub.Domain.Entities;
using Microsoft.AspNetCore.Identity;
namespace BakeryHub.Application.Interfaces;

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
}
