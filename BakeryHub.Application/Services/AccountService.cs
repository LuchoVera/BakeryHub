using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BakeryHub.Application.Services;
public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountService> _logger;

    private const string AdminRole = "Admin";
    private const string CustomerRole = "Customer";

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ITenantRepository tenantRepository,
        ApplicationDbContext context,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(AdminRegisterDto dto)
    {
        await EnsureRoleExistsAsync(AdminRole);
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email is already taken." }), null);
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
                    Subdomain = subdomainLower
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

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Admin user creation failed for email {Email}. Errors: {Errors}", dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return; 
                }

                var roleResult = await _userManager.AddToRoleAsync(user, AdminRole);
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to assign Admin role to user {UserId}. Errors: {Errors}", user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    result = roleResult;
                    return;
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Successfully registered Admin {UserId} for Tenant {TenantId}", user.Id, tenant.Id);

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Exception during Admin registration for email {Email}", dto.Email);
                result = IdentityResult.Failed(new IdentityError { Code = "RegistrationError", Description = "An unexpected error occurred during registration." });
            }
        });

        return (result, result.Succeeded ? user?.Id : null);
    }


    public async Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerAsync(CustomerRegisterDto dto)
    {
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email is already taken." }), null);
        }

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

        if (!result.Succeeded)
        {
            _logger.LogError("Customer user creation failed for email {Email}. Errors: {Errors}", dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return (result, null);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign Customer role to user {UserId}. Errors: {Errors}", user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return (roleResult, user.Id);
        }

            _logger.LogInformation("Successfully registered Customer {UserId}", user.Id);
        return (result, user.Id);
    }

    public async Task<SignInResult> LoginAsync(LoginDto dto)
    {
        var result = await _signInManager.PasswordSignInAsync(dto.Email, dto.Password, dto.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully.", dto.Email);
        }
        else if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} account locked out.", dto.Email);
        }
        else if (result.IsNotAllowed)
        {
            _logger.LogWarning("User {Email} login not allowed (Email/Phone not confirmed?).", dto.Email);
        }
        else
        {
            _logger.LogWarning("Invalid login attempt for user {Email}.", dto.Email);
        }


        return result;
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            _logger.LogInformation("Creating role: {RoleName}", roleName);
            await _roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }
}
