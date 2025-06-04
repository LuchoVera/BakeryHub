namespace BakeryHub.Domain.Projections;

public record OrderAggregationByEntity(Guid EntityId, string EntityName, decimal TotalAmount, int OrderCount);