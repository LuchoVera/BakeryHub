using BakeryHub.Application.Dtos;
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
        ApplicationDbContext context
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _context = context;
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAdminAsync(
        AdminRegisterDto dto
    )
    {
        await EnsureRoleExistsAsync(AdminRole);
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email is already taken.",
                    }
                ),
                null
            );
        }

        var subdomainLower = dto.Subdomain.ToLowerInvariant();
        if (await _tenantRepository.SubdomainExistsAsync(subdomainLower))
        {
            return (
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateSubdomain",
                        Description = "Subdomain is already taken.",
                    }
                ),
                null
            );
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
                    UpdatedAt = DateTimeOffset.UtcNow,
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
                    await transaction.RollbackAsync();
                    result = roleResult;
                    return;
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                result = IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "RegistrationError",
                        Description = "An unexpected error occurred during registration.",
                    }
                );
            }
        });

        return (result, result.Succeeded ? user?.Id : null);
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerAsync(
        CustomerRegisterDto dto
    )
    {
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email is already taken.",
                    }
                ),
                null
            );
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
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return (result, null);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded)
        {
            return (roleResult, user.Id);
        }

        return (result, user.Id);
    }

    public async Task<SignInResult> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return SignInResult.Failed;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            dto.Password,
            lockoutOnFailure: true
        );

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
                else
                {
                    return SignInResult.NotAllowed;
                }
            }
            else
            {
                var subdomain = dto.SubdomainContext.ToLowerInvariant();
                var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain);
                if (tenant == null)
                {
                    return SignInResult.Failed;
                }

                if (roles.Contains(CustomerRole))
                {
                    bool isMember = await _context.CustomerTenantMemberships.AnyAsync(m =>
                        m.ApplicationUserId == user.Id && m.TenantId == tenant.Id && m.IsActive
                    );

                    if (isMember)
                    {
                        await _signInManager.SignInAsync(user, dto.RememberMe);
                        return SignInResult.Success;
                    }
                    else
                    {
                        return SignInResult.NotAllowed;
                    }
                }
                else if (roles.Contains(AdminRole))
                {
                    return SignInResult.NotAllowed;
                }
                else
                {
                    return SignInResult.NotAllowed;
                }
            }
        }
        else
        {
            return result;
        }
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

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterCustomerForTenantAsync(
        CustomerRegisterDto dto,
        Guid tenantId
    )
    {
        await EnsureRoleExistsAsync(CustomerRole);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email is already taken.",
                    }
                ),
                null
            );
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
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        IdentityResult result = IdentityResult.Failed();
        Guid? createdUserId = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return;
                }
                createdUserId = user.Id;

                var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
                if (!roleResult.Succeeded)
                {
                    result = roleResult;
                    await transaction.RollbackAsync();
                    createdUserId = null;
                    return;
                }

                var membership = new CustomerTenantMembership
                {
                    ApplicationUserId = user.Id,
                    TenantId = tenantId,
                    DateJoined = DateTimeOffset.UtcNow,
                    IsActive = true,
                };
                _context.CustomerTenantMemberships.Add(membership);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                result = IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "RegistrationError",
                        Description = "An unexpected error occurred during registration.",
                    }
                );
                createdUserId = null;
            }
        });

        return (result, createdUserId);
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
            AdministeredTenantSubdomain = tenantSubdomain
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
            TenantMemberships = tenantMemberships
        };
    }
}
