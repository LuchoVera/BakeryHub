using BakeryHub.Modules.Orders.Domain.Enums;

namespace BakeryHub.Modules.Orders.Domain.Projections;

public record OrderAggregationByStatus(OrderStatus Status, decimal TotalAmount, int OrderCount);
