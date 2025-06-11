namespace BakeryHub.Domain.Projections;

public record OrderAggregationByTime(string PeriodLabel, decimal TotalAmount, int OrderCount);