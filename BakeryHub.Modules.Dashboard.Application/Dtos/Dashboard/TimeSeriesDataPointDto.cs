namespace BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;

public class TimeSeriesDataPointDto
{
    public string Label { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public decimal Value { get; set; }
    public int Count { get; set; }
}
