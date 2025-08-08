using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Tenants.Application.Dtos.Tenant;
using BakeryHub.Modules.Tenants.Application.Dtos.Theme;
using BakeryHub.Modules.Tenants.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Tenants.Application.Services;

public class TenantManagementService : ITenantManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantThemeRepository _tenantThemeRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public TenantManagementService(
        ITenantRepository tenantRepository,
        ITenantThemeRepository tenantThemeRepository,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantThemeRepository = tenantThemeRepository;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantDto?> GetTenantForAdminAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null)
        {
            return null;
        }

        var tenant = await _tenantRepository.GetByIdAsync(adminUser.TenantId.Value);

        if (tenant == null)
        {
            return null;
        }

        return new TenantDto { Id = tenant.Id, Name = tenant.Name, Subdomain = tenant.Subdomain };
    }

    public async Task<bool> UpdateTenantForAdminAsync(Guid adminUserId, UpdateTenantDto tenantDto)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null)
        {
            return false;
        }

        var tenant = await _tenantRepository.GetByIdAsync(adminUser.TenantId.Value);
        if (tenant == null)
        {
            return false;
        }
        tenant.Name = tenantDto.Name;

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private ThemeSettingsDto GetDefaultThemeSettings()
    {
        return new ThemeSettingsDto
        {
            ColorPrimary = "#0077b6",
            ColorPrimaryDark = "#023e8a",
            ColorPrimaryLight = "#ade8f4",
            ColorSecondary = "#eaf8fb",
            ColorBackground = "#f4f8f9",
            ColorSurface = "#ffffff",
            ColorTextPrimary = "#333333",
            ColorTextSecondary = "#5a5a5a",
            ColorTextOnPrimary = "#ffffff",
            ColorBorder = "#dce4e8",
            ColorBorderLight = "#e8f1f5",
            ColorDisabledBg = "#e9ecef"
        };
    }
    private TenantThemeDto GetDefaultTheme()
    {
        var defaultSettings = GetDefaultThemeSettings();
        return new TenantThemeDto
        {
            PublicTheme = defaultSettings,
            AdminTheme = defaultSettings
        };
    }

    public async Task<TenantThemeDto> GetThemeForAdminAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null)
        {
            return GetDefaultTheme();
        }

        var themeEntity = await _tenantThemeRepository.GetByTenantIdAsync(adminUser.TenantId.Value);

        if (themeEntity == null)
        {
            return GetDefaultTheme();
        }

        return new TenantThemeDto
        {
            PublicTheme = new ThemeSettingsDto
            {
                ColorPrimary = themeEntity.ColorPrimary,
                ColorPrimaryDark = themeEntity.ColorPrimaryDark,
                ColorPrimaryLight = themeEntity.ColorPrimaryLight,
                ColorSecondary = themeEntity.ColorSecondary,
                ColorBackground = themeEntity.ColorBackground,
                ColorSurface = themeEntity.ColorSurface,
                ColorTextPrimary = themeEntity.ColorTextPrimary,
                ColorTextSecondary = themeEntity.ColorTextSecondary,
                ColorTextOnPrimary = themeEntity.ColorTextOnPrimary,
                ColorBorder = themeEntity.ColorBorder,
                ColorBorderLight = themeEntity.ColorBorderLight,
                ColorDisabledBg = themeEntity.ColorDisabledBg
            },
            AdminTheme = new ThemeSettingsDto
            {
                ColorPrimary = themeEntity.AdminColorPrimary,
                ColorPrimaryDark = themeEntity.AdminColorPrimaryDark,
                ColorPrimaryLight = themeEntity.AdminColorPrimaryLight,
                ColorSecondary = themeEntity.AdminColorSecondary,
                ColorBackground = themeEntity.AdminColorBackground,
                ColorSurface = themeEntity.AdminColorSurface,
                ColorTextPrimary = themeEntity.AdminColorTextPrimary,
                ColorTextSecondary = themeEntity.AdminColorTextSecondary,
                ColorTextOnPrimary = themeEntity.AdminColorTextOnPrimary,
                ColorBorder = themeEntity.AdminColorBorder,
                ColorBorderLight = themeEntity.AdminColorBorderLight,
                ColorDisabledBg = themeEntity.AdminColorDisabledBg
            }
        };
    }

    public async Task<bool> UpdateThemeForAdminAsync(Guid adminUserId, TenantThemeDto themeDto)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null) return false;

        var themeEntity = await _tenantThemeRepository.GetByTenantIdAsync(adminUser.TenantId.Value, trackEntity: true);

        if (themeEntity == null)
        {
            themeEntity = new TenantTheme { TenantId = adminUser.TenantId.Value };
            await _tenantThemeRepository.AddAsync(themeEntity);
        }

        themeEntity.ColorPrimary = themeDto.PublicTheme.ColorPrimary;
        themeEntity.ColorPrimaryDark = themeDto.PublicTheme.ColorPrimaryDark;
        themeEntity.ColorPrimaryLight = themeDto.PublicTheme.ColorPrimaryLight;
        themeEntity.ColorSecondary = themeDto.PublicTheme.ColorSecondary;
        themeEntity.ColorBackground = themeDto.PublicTheme.ColorBackground;
        themeEntity.ColorSurface = themeDto.PublicTheme.ColorSurface;
        themeEntity.ColorTextPrimary = themeDto.PublicTheme.ColorTextPrimary;
        themeEntity.ColorTextSecondary = themeDto.PublicTheme.ColorTextSecondary;
        themeEntity.ColorTextOnPrimary = themeDto.PublicTheme.ColorTextOnPrimary;
        themeEntity.ColorBorder = themeDto.PublicTheme.ColorBorder;
        themeEntity.ColorBorderLight = themeDto.PublicTheme.ColorBorderLight;
        themeEntity.ColorDisabledBg = themeDto.PublicTheme.ColorDisabledBg;

        themeEntity.AdminColorPrimary = themeDto.AdminTheme.ColorPrimary;
        themeEntity.AdminColorPrimaryDark = themeDto.AdminTheme.ColorPrimaryDark;
        themeEntity.AdminColorPrimaryLight = themeDto.AdminTheme.ColorPrimaryLight;
        themeEntity.AdminColorSecondary = themeDto.AdminTheme.ColorSecondary;
        themeEntity.AdminColorBackground = themeDto.AdminTheme.ColorBackground;
        themeEntity.AdminColorSurface = themeDto.AdminTheme.ColorSurface;
        themeEntity.AdminColorTextPrimary = themeDto.AdminTheme.ColorTextPrimary;
        themeEntity.AdminColorTextSecondary = themeDto.AdminTheme.ColorTextSecondary;
        themeEntity.AdminColorTextOnPrimary = themeDto.AdminTheme.ColorTextOnPrimary;
        themeEntity.AdminColorBorder = themeDto.AdminTheme.ColorBorder;
        themeEntity.AdminColorBorderLight = themeDto.AdminTheme.ColorBorderLight;
        themeEntity.AdminColorDisabledBg = themeDto.AdminTheme.ColorDisabledBg;

        themeEntity.UpdatedAt = DateTimeOffset.UtcNow;
        _tenantThemeRepository.Update(themeEntity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPublicThemeAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null) return false;

        var themeEntity = await _tenantThemeRepository.GetByTenantIdAsync(adminUser.TenantId.Value, trackEntity: true);
        var defaultSettings = GetDefaultThemeSettings();

        if (themeEntity == null)
        {
            return true;
        }

        themeEntity.ColorPrimary = defaultSettings.ColorPrimary;
        themeEntity.ColorPrimaryDark = defaultSettings.ColorPrimaryDark;
        themeEntity.ColorPrimaryLight = defaultSettings.ColorPrimaryLight;
        themeEntity.ColorSecondary = defaultSettings.ColorSecondary;
        themeEntity.ColorBackground = defaultSettings.ColorBackground;
        themeEntity.ColorSurface = defaultSettings.ColorSurface;
        themeEntity.ColorTextPrimary = defaultSettings.ColorTextPrimary;
        themeEntity.ColorTextSecondary = defaultSettings.ColorTextSecondary;
        themeEntity.ColorTextOnPrimary = defaultSettings.ColorTextOnPrimary;
        themeEntity.ColorBorder = defaultSettings.ColorBorder;
        themeEntity.ColorBorderLight = defaultSettings.ColorBorderLight;
        themeEntity.ColorDisabledBg = defaultSettings.ColorDisabledBg;

        themeEntity.UpdatedAt = DateTimeOffset.UtcNow;
        _tenantThemeRepository.Update(themeEntity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetAdminThemeAsync(Guid adminUserId)
    {
        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString());
        if (adminUser?.TenantId == null) return false;

        var themeEntity = await _tenantThemeRepository.GetByTenantIdAsync(adminUser.TenantId.Value, trackEntity: true);
        var defaultSettings = GetDefaultThemeSettings();

        if (themeEntity == null)
        {
            return true;
        }

        themeEntity.AdminColorPrimary = defaultSettings.ColorPrimary;
        themeEntity.AdminColorPrimaryDark = defaultSettings.ColorPrimaryDark;
        themeEntity.AdminColorPrimaryLight = defaultSettings.ColorPrimaryLight;
        themeEntity.AdminColorSecondary = defaultSettings.ColorSecondary;
        themeEntity.AdminColorBackground = defaultSettings.ColorBackground;
        themeEntity.AdminColorSurface = defaultSettings.ColorSurface;
        themeEntity.AdminColorTextPrimary = defaultSettings.ColorTextPrimary;
        themeEntity.AdminColorTextSecondary = defaultSettings.ColorTextSecondary;
        themeEntity.AdminColorTextOnPrimary = defaultSettings.ColorTextOnPrimary;
        themeEntity.AdminColorBorder = defaultSettings.ColorBorder;
        themeEntity.AdminColorBorderLight = defaultSettings.ColorBorderLight;
        themeEntity.AdminColorDisabledBg = defaultSettings.ColorDisabledBg;

        themeEntity.UpdatedAt = DateTimeOffset.UtcNow;
        _tenantThemeRepository.Update(themeEntity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
