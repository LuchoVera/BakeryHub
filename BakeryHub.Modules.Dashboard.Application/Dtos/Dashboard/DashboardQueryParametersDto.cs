namespace BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;

public class DashboardQueryParametersDto
{
    public string TimePeriod { get; set; } = "last7days";
    public DateTimeOffset? CustomStartDate { get; set; }
    public DateTimeOffset? CustomEndDate { get; set; }
    public string Granularity { get; set; } = "daily";
    public string Metric { get; set; } = "revenue";
    public string? FilterDimension { get; set; }
    public string? FilterValue { get; set; }
    public string? BreakdownDimension { get; set; }
    public bool IncludeProductsWithNoSales { get; set; } = false;
}
