namespace BakeryHub.Application.Dtos.Dashboard;

public class AggregatedDataSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue => TotalOrders > 0 ? TotalRevenue / TotalOrders : 0;
    public int TotalCustomers { get; set; }
}