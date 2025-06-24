namespace BakeryHub.Application.Dtos.Theme;

public class TenantThemeDto
{
    public ThemeSettingsDto PublicTheme { get; set; } = new();
    public ThemeSettingsDto AdminTheme { get; set; } = new();
}
