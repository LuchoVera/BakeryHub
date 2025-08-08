namespace BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;

public class AvailableDrillOptionDto
{
    public required string DimensionName { get; set; }
    public required string DisplayName { get; set; }
    public bool IsBreakdownDimension { get; set; } = true;
    public string? TargetGranularity { get; set; }
}
