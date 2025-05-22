using BakeryHub.Application.Dtos;
using BakeryHub.Application.Dtos.Enums;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Application.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ApplicationDbContext _context;
    private const string AdminRole = "Admin";
    private const string CustomerRole = "Customer";

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ITenantRepository tenantRepository,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _context = context;
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(AdminRegisterDto dto)
    {
        await EnsureRoleExistsAsync(AdminRole);
        await EnsureRoleExistsAsync(CustomerRole);

        var normalizedEmail = _userManager.NormalizeEmail(dto.Email);
        bool emailExists = await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (emailExists)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateAdminEmail", Description = "This email address is already registered." }), null);
        }

        var subdomainLower = dto.Subdomain.ToLowerInvariant();
        if (await _tenantRepository.SubdomainExistsAsync(subdomainLower))
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateSubdomain", Description = "Subdomain is already taken." }), null);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        IdentityResult result = IdentityResult.Failed();
        ApplicationUser? user = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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
                await _context.SaveChangesAsync();

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
                if (!result.Succeeded) { await transaction.RollbackAsync(); return; }

                var roleResult = await _userManager.AddToRoleAsync(user, AdminRole);
                if (!roleResult.Succeeded) { result = roleResult; await transaction.RollbackAsync(); return; }

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

        var defaultFailedResult = new DetailedRegistrationResult { Outcome = RegistrationOutcome.Failed };

        var tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId);
        if (!tenantExists)
        {
            defaultFailedResult.Outcome = RegistrationOutcome.TenantNotFound;
            defaultFailedResult.IdentityResult = IdentityResult.Failed(new IdentityError { Code = "TenantNotFound", Description = "Specified tenant does not exist." });
            return defaultFailedResult;
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);

        if (existingUser != null)
        {
            bool isAlreadyMember = await _context.CustomerTenantMemberships
                .AnyAsync(m => m.ApplicationUserId == existingUser.Id && m.TenantId == tenantId && m.IsActive);

            if (isAlreadyMember)
            {
                defaultFailedResult.Outcome = RegistrationOutcome.AlreadyMember;
                defaultFailedResult.IdentityResult = IdentityResult.Failed(new IdentityError { Code = "AlreadyMember", Description = "You are already registered with this bakery." });
                defaultFailedResult.UserId = existingUser.Id;
                return defaultFailedResult;
            }
            else
            {
                var roles = await _userManager.GetRolesAsync(existingUser);
                IdentityResult roleResult = IdentityResult.Success;
                if (!roles.Contains(CustomerRole))
                {
                    roleResult = await _userManager.AddToRoleAsync(existingUser, CustomerRole);
                    if (!roleResult.Succeeded)
                    {
                        return new DetailedRegistrationResult { IdentityResult = roleResult, UserId = existingUser.Id, Outcome = RegistrationOutcome.RoleAssignmentFailed };
                    }
                }
                var membership = new CustomerTenantMembership { ApplicationUserId = existingUser.Id, TenantId = tenantId, IsActive = true, DateJoined = DateTimeOffset.UtcNow };
                _context.CustomerTenantMemberships.Add(membership);
                try
                {
                    await _context.SaveChangesAsync();
                    return new DetailedRegistrationResult { IdentityResult = IdentityResult.Success, UserId = existingUser.Id, Outcome = RegistrationOutcome.MembershipCreated };
                }
                catch
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
            var strategy = _context.Database.CreateExecutionStrategy();
            IdentityResult result = IdentityResult.Failed();
            Guid? createdUserId = null;
            RegistrationOutcome outcome = RegistrationOutcome.Failed;

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    result = await _userManager.CreateAsync(user, dto.Password);
                    if (!result.Succeeded) { await transaction.RollbackAsync(); outcome = RegistrationOutcome.Failed; return; }
                    createdUserId = user.Id;
                    var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
                    if (!roleResult.Succeeded) { result = roleResult; await transaction.RollbackAsync(); createdUserId = null; outcome = RegistrationOutcome.RoleAssignmentFailed; return; }
                    var membership = new CustomerTenantMembership { ApplicationUserId = user.Id, TenantId = tenantId, IsActive = true, DateJoined = DateTimeOffset.UtcNow };
                    _context.CustomerTenantMemberships.Add(membership);
                    await _context.SaveChangesAsync();
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
                    bool isMember = await _context.CustomerTenantMemberships.AnyAsync(m => m.ApplicationUserId == user.Id && m.TenantId == tenant.Id && m.IsActive);
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
            var result = await _roleManager.CreateAsync(new ApplicationRole(roleName));
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
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);
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
    public async Task<AuthUserDto> GetCurrentUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        string? administeredTenantSubdomain = null;
        Guid? administeredTenantId = null;
        List<Guid> tenantMemberships = new List<Guid>();

        if (user.TenantId != null && roles.Contains("Admin"))
        {
            administeredTenantId = user.TenantId;
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);
            administeredTenantSubdomain = tenant?.Subdomain;
        }

        if (roles.Contains("Customer"))
        {
            tenantMemberships = await _context.CustomerTenantMemberships
                .Where(m => m.ApplicationUserId == user.Id && m.IsActive)
                .Select(m => m.TenantId)
                .ToListAsync();
        }

        return new AuthUserDto
        {
            UserId = user.Id,
            Email = user.Email!,
            Name = user.Name,
            Roles = roles,
            AdministeredTenantId = administeredTenantId,
            AdministeredTenantSubdomain = administeredTenantSubdomain,
            TenantMemberships = tenantMemberships,
            PhoneNumber = user.PhoneNumber
        };
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

        bool tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId);
        if (!tenantExists)
        {
            return new LinkAccountResult
            {
                Outcome = LinkAccountOutcome.TenantNotFound,
                IdentityResult = IdentityResult
            .Failed(new IdentityError { Code = "TenantNotFound", Description = "Bakery not found." })
            };
        }

        bool isAlreadyMember = await _context.CustomerTenantMemberships
            .AnyAsync(m => m.ApplicationUserId == user.Id && m.TenantId == tenantId);

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

        _context.CustomerTenantMemberships.Add(membership);

        try
        {
            await _context.SaveChangesAsync();
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
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "Usuario no encontrado." });
        }
        return await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
    }

    public async Task<(IdentityResult Result, AuthUserDto? UpdatedUser)> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "Usuario no encontrado." }), null);
        }

        user.Name = dto.Name;
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (result, null);
        }

        var updatedAuthUser = await GetCurrentUserAsync(user);
        return (result, updatedAuthUser);
    }
}
