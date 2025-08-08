namespace BakeryHub.Modules.Orders.Domain.Projections;

public record OrderAggregationByEntity(Guid EntityId, string EntityName, decimal TotalAmount, int OrderCount);
