using BakeryHub.Application.Dtos;
using Microsoft.AspNetCore.Identity;
namespace BakeryHub.Application.Interfaces;
public interface IAccountService
{
    Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(AdminRegisterDto dto);
    Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerAsync(CustomerRegisterDto dto);
    Task<SignInResult> LoginAsync(LoginDto dto);
    Task LogoutAsync();
}
