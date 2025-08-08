namespace BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;

public class DashboardResponseDto
{
    public string Title { get; set; } = string.Empty;
    public string PeriodDescription { get; set; } = string.Empty;
    public AggregatedDataSummaryDto Summary { get; set; } = new();
    public List<TimeSeriesDataPointDto> Breakdown { get; set; } = new();
    public List<AvailableDrillOptionDto> NextDrillOptions { get; set; } = new();
}
