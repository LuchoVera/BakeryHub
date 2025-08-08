namespace BakeryHub.Modules.Orders.Domain.Projections;

public record OrderAggregationByTime(string PeriodLabel, decimal TotalAmount, int OrderCount);
