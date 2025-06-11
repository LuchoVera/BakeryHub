
using BakeryHub.Domain.Enums;
namespace BakeryHub.Domain.Projections;

public record OrderAggregationByStatus(OrderStatus Status, decimal TotalAmount, int OrderCount);