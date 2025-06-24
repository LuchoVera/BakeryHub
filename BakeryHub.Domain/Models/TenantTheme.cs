using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Domain.Entities;

public class TenantTheme
{
    [Key]
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ColorPrimary { get; set; } = "#0077b6";
    public string ColorPrimaryDark { get; set; } = "#023e8a";
    public string ColorPrimaryLight { get; set; } = "#ade8f4";
    public string ColorSecondary { get; set; } = "#eaf8fb";
    public string ColorBackground { get; set; } = "#f4f8f9";
    public string ColorSurface { get; set; } = "#ffffff";
    public string ColorTextPrimary { get; set; } = "#333333";
    public string ColorTextSecondary { get; set; } = "#5a5a5a";
    public string ColorTextOnPrimary { get; set; } = "#ffffff";
    public string ColorBorder { get; set; } = "#dce4e8";
    public string ColorBorderLight { get; set; } = "#e8f1f5";
    public string ColorDisabledBg { get; set; } = "#e9ecef";

    public string AdminColorPrimary { get; set; } = "#4a4e69";
    public string AdminColorPrimaryDark { get; set; } = "#22223b";
    public string AdminColorPrimaryLight { get; set; } = "#9a8c98";
    public string AdminColorSecondary { get; set; } = "#f2e9e4";
    public string AdminColorBackground { get; set; } = "#f8f7ff";
    public string AdminColorSurface { get; set; } = "#ffffff";
    public string AdminColorTextPrimary { get; set; } = "#22223b";
    public string AdminColorTextSecondary { get; set; } = "#4a4e69";
    public string AdminColorTextOnPrimary { get; set; } = "#ffffff";
    public string AdminColorBorder { get; set; } = "#c9ada7";
    public string AdminColorBorderLight { get; set; } = "#e2d6d4";
    public string AdminColorDisabledBg { get; set; } = "#e9ecef";
}
