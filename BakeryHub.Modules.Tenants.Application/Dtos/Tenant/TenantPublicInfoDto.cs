using BakeryHub.Modules.Tenants.Application.Dtos.Theme;

namespace BakeryHub.Modules.Tenants.Application.Dtos.Tenant;

public class TenantPublicInfoDto
{
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public string? PhoneNumber { get; set; }
    public ThemeSettingsDto? Theme { get; set; }
}
