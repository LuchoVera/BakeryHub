using BakeryHub.Application.Dtos;
using BakeryHub.Application.Dtos.Dashboard;
using BakeryHub.Domain.Enums;

namespace BakeryHub.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto?> CreateOrderAsync(Guid tenantId, Guid applicationUserId, CreateOrderDto createOrderDto);
    Task<OrderDto?> GetOrderDetailsForCustomerAsync(Guid orderId, Guid userId, Guid tenantId);
    Task<OrderDto?> GetOrderDetailsForAdminAsync(Guid orderId, Guid tenantId);
    Task<IEnumerable<OrderDto>> GetOrderHistoryForCustomerAsync(Guid userId, Guid tenantId);
    Task<IEnumerable<OrderDto>> GetOrdersForAdminAsync(Guid tenantId);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid tenantId, OrderStatus newStatus, Guid adminUserId);
    Task<DashboardResponseDto> GetDashboardStatisticsAsync(Guid tenantId, DashboardQueryParametersDto queryParams);
}
