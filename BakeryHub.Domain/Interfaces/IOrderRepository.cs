using BakeryHub.Domain.Entities;

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
}
