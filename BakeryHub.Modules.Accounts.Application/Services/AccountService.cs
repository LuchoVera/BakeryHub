using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Application.Dtos.Admin;
using BakeryHub.Modules.Accounts.Application.Dtos.Auth;
using BakeryHub.Modules.Accounts.Application.Dtos.Customer;
using BakeryHub.Modules.Accounts.Application.Dtos.Enums;
using BakeryHub.Modules.Accounts.Application.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Shared.Kernel.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Accounts.Application.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ICustomerTenantMembershipRepository _customerTenantMembershipRepository;
    private const string AdminRole = "Admin";
    private const string CustomerRole = "Customer";

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ICustomerTenantMembershipRepository customerTenantMembershipRepository)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _customerTenantMembershipRepository = customerTenantMembershipRepository;
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(AdminRegisterDto dto)
    {
        await EnsureRoleExistsAsync(AdminRole);
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateAdminEmail", Description = "This email address is already registered." }), null);
        }

        var subdomainLower = dto.Subdomain.ToLowerInvariant();
        if (await _tenantRepository.SubdomainExistsAsync(subdomainLower))
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateSubdomain", Description = "Subdomain is already taken." }), null);
        }

        IdentityResult result = IdentityResult.Failed();
        ApplicationUser? user = null;

        await _unitOfWork.ExecuteStrategyAsync(async () =>
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = dto.BusinessName,
                    Subdomain = subdomainLower,
                    PhoneNumber = dto.PhoneNumber
                };
                await _tenantRepository.AddAsync(tenant);
                await _unitOfWork.SaveChangesAsync();

                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Name = dto.AdminName,
                    Email = dto.Email,
                    UserName = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    EmailConfirmed = false,
                    TenantId = tenant.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return;
                }

                var roleResult = await _userManager.AddToRoleAsync(user, AdminRole);
                if (!roleResult.Succeeded)
                {
                    result = roleResult;
                    await transaction.RollbackAsync();
                    return;
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                result = IdentityResult.Failed(new IdentityError { Code = "RegistrationError", Description = "An unexpected error occurred during registration." });
                user = null;
            }
        });

        return (result, result.Succeeded ? user?.Id : null);
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerAsync(CustomerRegisterDto dto)
    {
        await EnsureRoleExistsAsync(CustomerRole);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            UserName = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            EmailConfirmed = false,
            TenantId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded) { return (result, null); }
        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded) { return (roleResult, user.Id); }
        return (result, user.Id);
    }

    public async Task<DetailedRegistrationResult> RegisterCustomerForTenantAsync(CustomerRegisterDto dto, Guid tenantId)
    {
        await EnsureRoleExistsAsync(CustomerRole);

        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            return new DetailedRegistrationResult
            {
                Outcome = RegistrationOutcome.TenantNotFound,
                IdentityResult = IdentityResult.Failed(new IdentityError { Code = "TenantNotFound", Description = "Specified tenant does not exist." })
            };
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);

        if (existingUser != null)
        {
            bool isAlreadyMember = await _customerTenantMembershipRepository.IsMemberAsync(existingUser.Id, tenantId);

            if (isAlreadyMember)
            {
                return new DetailedRegistrationResult
                {
                    Outcome = RegistrationOutcome.AlreadyMember,
                    IdentityResult = IdentityResult.Failed(new IdentityError { Code = "AlreadyMember", Description = "You are already registered with this bakery." }),
                    UserId = existingUser.Id
                };
            }
            else
            {
                var roles = await _userManager.GetRolesAsync(existingUser);
                if (!roles.Contains(CustomerRole))
                {
                    var roleResult = await _userManager.AddToRoleAsync(existingUser, CustomerRole);
                    if (!roleResult.Succeeded)
                    {
                        return new DetailedRegistrationResult { IdentityResult = roleResult, UserId = existingUser.Id, Outcome = RegistrationOutcome.RoleAssignmentFailed };
                    }
                }

                var membership = new CustomerTenantMembership { ApplicationUserId = existingUser.Id, TenantId = tenantId, IsActive = true, DateJoined = DateTimeOffset.UtcNow };
                await _customerTenantMembershipRepository.AddAsync(membership);

                try
                {
                    await _unitOfWork.SaveChangesAsync();
                    return new DetailedRegistrationResult { IdentityResult = IdentityResult.Success, UserId = existingUser.Id, Outcome = RegistrationOutcome.MembershipCreated };
                }
                catch (DbUpdateException)
                {
                    return new DetailedRegistrationResult { Outcome = RegistrationOutcome.UnknownError, IdentityResult = IdentityResult.Failed(new IdentityError { Code = "DbError", Description = "Database error creating membership." }) };
                }
            }
        }
        else
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Email = dto.Email,
                UserName = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                EmailConfirmed = false,
                TenantId = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            IdentityResult result = IdentityResult.Failed();
            Guid? createdUserId = null;
            RegistrationOutcome outcome = RegistrationOutcome.Failed;

            await _unitOfWork.ExecuteStrategyAsync(async () =>
            {
                await using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    result = await _userManager.CreateAsync(user, dto.Password);
                    if (!result.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        outcome = RegistrationOutcome.Failed;
                        return;
                    }

                    createdUserId = user.Id;
                    var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
                    if (!roleResult.Succeeded)
                    {
                        result = roleResult;
                        await transaction.RollbackAsync();
                        createdUserId = null;
                        outcome = RegistrationOutcome.RoleAssignmentFailed;
                        return;
                    }

                    var membership = new CustomerTenantMembership { ApplicationUserId = user.Id, TenantId = tenantId, IsActive = true, DateJoined = DateTimeOffset.UtcNow };
                    await _customerTenantMembershipRepository.AddAsync(membership);

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                    outcome = RegistrationOutcome.UserCreated;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    result = IdentityResult.Failed(new IdentityError { Code = "DbError", Description = "Error during signup." });
                    createdUserId = null;
                    outcome = RegistrationOutcome.UnknownError;
                }
            });

            return new DetailedRegistrationResult { IdentityResult = result, UserId = createdUserId, Outcome = outcome };
        }
    }
    public async Task<SignInResult> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null) { return SignInResult.Failed; }
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (string.IsNullOrWhiteSpace(dto.SubdomainContext))
            {
                if (roles.Contains(AdminRole))
                {
                    await _signInManager.SignInAsync(user, dto.RememberMe);
                    return SignInResult.Success;
                }
                else { return SignInResult.NotAllowed; }
            }
            else
            {
                var subdomain = dto.SubdomainContext.ToLowerInvariant();
                var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain);
                if (tenant == null) { return SignInResult.Failed; }
                if (roles.Contains(CustomerRole))
                {
                    bool isMember = await _customerTenantMembershipRepository.IsMemberAsync(user.Id, tenant.Id);
                    if (isMember)
                    {
                        await _signInManager.SignInAsync(user, dto.RememberMe);
                        return SignInResult.Success;
                    }
                    else { return SignInResult.NotAllowed; }
                }
                else { return SignInResult.NotAllowed; }
            }
        }
        else { return result; }
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }

    public async Task<AuthResponseDto> LoginAndBuildResponseAsync(LoginDto loginDto)
    {
        var result = await LoginAsync(loginDto);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Login failed. Please check your credentials.");
        }

        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            throw new InvalidOperationException("Login succeeded but failed to retrieve user details.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        string? tenantSubdomain = null;
        Guid? tenantId = null;

        if (user.TenantId != null && roles.Contains("Admin"))
        {
            tenantId = user.TenantId;
            var tenant = await _tenantRepository.GetByIdAsync(user.TenantId.Value);
            tenantSubdomain = tenant?.Subdomain;
        }

        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email!,
            Name = user.Name,
            Roles = roles,
            AdministeredTenantId = tenantId,
            AdministeredTenantSubdomain = tenantSubdomain,
            PhoneNumber = user.PhoneNumber
        };
    }
    public async Task<AuthUserDto?> GetCurrentUserAsync(ApplicationUser user, string? subdomainContext)
    {
        var roles = await _userManager.GetRolesAsync(user);
        if (string.IsNullOrWhiteSpace(subdomainContext))
        {
            if (roles.Contains(AdminRole))
            {
                string? administeredTenantSubdomain = null;
                Guid? administeredTenantId = null;

                if (user.TenantId != null)
                {
                    administeredTenantId = user.TenantId;
                    var tenant = await _tenantRepository.GetByIdAsync(user.TenantId.Value);
                    administeredTenantSubdomain = tenant?.Subdomain;
                }

                return new AuthUserDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Name = user.Name,
                    Roles = roles,
                    AdministeredTenantId = administeredTenantId,
                    AdministeredTenantSubdomain = administeredTenantSubdomain,
                    TenantMemberships = new List<Guid>(),
                    PhoneNumber = user.PhoneNumber
                };
            }
            else
            {
                return null;
            }
        }
        else
        {
            if (roles.Contains(CustomerRole))
            {
                var tenant = await _tenantRepository.GetBySubdomainAsync(subdomainContext);
                if (tenant == null) return null;

                bool isMember = await _customerTenantMembershipRepository.IsMemberAsync(user.Id, tenant.Id);

                if (isMember)
                {
                    return new AuthUserDto
                    {
                        UserId = user.Id,
                        Email = user.Email!,
                        Name = user.Name,
                        Roles = roles,
                        TenantMemberships = new List<Guid> { tenant.Id },
                        PhoneNumber = user.PhoneNumber
                    };
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }

    public async Task<EmailCheckResultDto> CheckEmailAsync(string email)
    {
        var result = new EmailCheckResultDto { Exists = false };
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            result.Exists = true;
            result.Name = user.Name;
            var roles = await _userManager.GetRolesAsync(user);
            result.IsAdmin = roles.Contains(AdminRole);
            result.IsCustomer = roles.Contains(CustomerRole);
        }
        return result;
    }
    public async Task<LinkAccountResult> LinkExistingCustomerToTenantAsync(string email, Guid tenantId)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.UserNotFound,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "UserNotFound", Description = "Email address not found." })
            };
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(AdminRole))
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.AdminConflict,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "AdminConflict", Description = "Administrators cannot be linked as customers." })
            };
        }

        if (!roles.Contains(CustomerRole))
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.UserNotCustomer,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "UserNotCustomer", Description = "This account is not a customer account." })
            };
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.TenantNotFound,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "TenantNotFound", Description = "Bakery not found." })
            };
        }

        bool isAlreadyMember = await _customerTenantMembershipRepository.IsMemberAsync(user.Id, tenantId);

        if (isAlreadyMember)
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.AlreadyMember,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "AlreadyMember", Description = "Account is already linked to this bakery." })
            };
        }

        var membership = new CustomerTenantMembership
        {
            ApplicationUserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
            DateJoined = DateTimeOffset.UtcNow
        };

        await _customerTenantMembershipRepository.AddAsync(membership);

        try
        {
            await _unitOfWork.SaveChangesAsync();
            return new LinkAccountResult { Outcome = LinkAccountOutcome.Linked, IdentityResult = IdentityResult.Success };
        }
        catch (DbUpdateException)
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.DbError,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "DbError", Description = "A database error occurred while linking the account." })
            };
        }
        catch
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.Failed,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "UnknownError", Description = "An unexpected error occurred during linking." })
            };
        }
    }

    public async Task<IdentityResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User not found." });
        }

        var isSameAsOldPassword = await _userManager.CheckPasswordAsync(user, dto.NewPassword);
        if (isSameAsOldPassword)
        {
            return IdentityResult.Failed(new IdentityError { Code = "SameAsOldPassword", Description = "The new password cannot be the same as the current password." });
        }

        return await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
    }

    public async Task<(IdentityResult Result, AuthUserDto? UpdatedUser)> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto, string? subdomainContext)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User not found." }), null);
        }

        user.Name = dto.Name;
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (result, null);
        }

        var updatedAuthUser = await GetCurrentUserAsync(user, subdomainContext);

        return (result, updatedAuthUser);
    }

    public async Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return IdentityResult.Success;
        }

        var token = new Random().Next(100000, 999999).ToString();
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpirationDate = DateTimeOffset.UtcNow.AddMinutes(10);

        await _userManager.UpdateAsync(user);

        var emailBody = $"<h1>Restablece tu Contraseña</h1>" +
                        $"<p>Usa el siguiente código para restablecer tu contraseña. El código es válido por 10 minutos:</p>" +
                        $"<h2>{token}</h2>" +
                        $"<p>Si no solicitaste esto, puedes ignorar este correo.</p>";

        await _emailService.SendEmailAsync(user.Email, "Código para restablecer tu contraseña", emailBody);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "InvalidRequest", Description = "Invalid password reset request." });
        }

        if (user.PasswordResetToken != dto.Token || user.PasswordResetTokenExpirationDate <= DateTimeOffset.UtcNow)
        {
            return IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "The code is invalid or has expired." });
        }

        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, identityToken, dto.NewPassword);

        if (result.Succeeded)
        {
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpirationDate = null;
            await _userManager.UpdateAsync(user);
        }

        return result;
    }

    public async Task<(IdentityResult Result, AuthUserDto? UpdatedUser)> UpdateAdminProfileAsync(Guid adminUserId, UpdateAdminProfileDto dto)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser == null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "UserNotFound" }), null);
        }

        if (adminUser.TenantId == null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "TenantNotFound" }), null);
        }

        var tenant = await _tenantRepository.GetByIdAsync(adminUser.TenantId.Value);
        if (tenant == null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "TenantNotFound" }), null);
        }

        adminUser.Name = dto.AdminName;
        adminUser.PhoneNumber = dto.PhoneNumber;
        adminUser.UpdatedAt = DateTimeOffset.UtcNow;

        var userUpdateResult = await _userManager.UpdateAsync(adminUser);
        if (!userUpdateResult.Succeeded)
        {
            return (userUpdateResult, null);
        }

        tenant.Name = dto.BusinessName;
        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync();

        var updatedAuthUser = await GetCurrentUserAsync(adminUser, null);

        return (IdentityResult.Success, updatedAuthUser);
    }
}
