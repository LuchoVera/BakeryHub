using BakeryHub.Modules.Orders.Application.Dtos.Order;
using BakeryHub.Modules.Orders.Domain.Enums;

namespace BakeryHub.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto?> CreateOrderAsync(Guid tenantId, Guid applicationUserId, CreateOrderDto createOrderDto);
    Task<OrderDto?> GetOrderDetailsForCustomerAsync(Guid orderId, Guid userId, Guid tenantId);
    Task<OrderDto?> GetOrderDetailsForAdminAsync(Guid orderId, Guid tenantId);
    Task<IEnumerable<OrderDto>> GetOrderHistoryForCustomerAsync(Guid userId, Guid tenantId);
    Task<IEnumerable<OrderDto>> GetOrdersForAdminAsync(Guid tenantId);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid tenantId, OrderStatus newStatus, Guid adminUserId);
    Task<OrderDto?> CreateManualOrderForAdminAsync(Guid tenantId, CreateManualOrderDto createManualOrderDto);
}
