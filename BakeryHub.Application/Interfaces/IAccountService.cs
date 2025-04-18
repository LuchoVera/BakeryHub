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
    Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerForTenantAsync(CustomerRegisterDto dto, Guid tenantId);
    Task<AuthResponseDto> LoginAndBuildResponseAsync(LoginDto loginDto);
    Task<AuthUserDto> GetCurrentUserAsync(ApplicationUser user);
}
