using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Projections;

namespace BakeryHub.Domain.Interfaces;

public interface IOrderRepository
{
    Task AddOrderAsync(Order order);
    Task<Order?> GetOrderByIdAndTenantAsync(Guid orderId, Guid tenantId);
    Task<Order?> GetOrderByIdAndUserAsync(Guid orderId, Guid userId, Guid tenantId);
    Task<IEnumerable<Order>> GetOrdersByUserIdAndTenantAsync(Guid userId, Guid tenantId);
    Task<IEnumerable<Order>> GetOrdersByTenantIdAsync(Guid tenantId);
    void UpdateOrder(Order order);
    Task<IEnumerable<Order>> GetOrdersWithItemsAndProductCategoriesAsync(Guid userId, Guid tenantId);
    Task<(decimal TotalRevenue, int TotalOrders, int TotalUniqueCustomers)> GetOverallSummaryAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery);
    Task<List<OrderAggregationByTime>> GetOrdersAggregatedByTimeDimensionAsync(
        Guid tenantId, DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeDimension, IQueryable<Order> preFilteredOrdersQuery);
    Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCategoryAsync(
        Guid tenantId, DateTimeOffset startDate,
        DateTimeOffset endDate,
        IQueryable<Order> preFilteredOrdersQuery);
    Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByProductAsync(
        Guid tenantId, DateTimeOffset startDate,
        DateTimeOffset endDate,
        IQueryable<Order> preFilteredOrdersQuery,
        bool includeProductsWithNoSales);
    Task<List<OrderAggregationByStatus>> GetOrdersAggregatedByStatusAsync(
        Guid tenantId, DateTimeOffset startDate,
        DateTimeOffset endDate,
        IQueryable<Order> preFilteredOrdersQuery);
    Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCustomerAsync(
        Guid tenantId, DateTimeOffset startDate,
        DateTimeOffset endDate,
        IQueryable<Order> preFilteredOrdersQuery);
    IQueryable<Order> GetFilteredOrdersQuery(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate,
        string? filterDimension = null, string? filterValue = null);
}
