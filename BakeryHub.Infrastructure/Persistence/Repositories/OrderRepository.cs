using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddOrderAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
    }

    public async Task<Order?> GetOrderByIdAndTenantAsync(Guid orderId, Guid tenantId)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);
    }

    public async Task<Order?> GetOrderByIdAndUserAsync(Guid orderId, Guid userId, Guid tenantId)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ApplicationUserId == userId && o.TenantId == tenantId);
    }

    public async Task<IEnumerable<Order>> GetOrdersByUserIdAndTenantAsync(Guid userId, Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.ApplicationUserId == userId && o.TenantId == tenantId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersByTenantIdAsync(Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public void UpdateOrder(Order order)
    {
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Orders.Update(order);
    }

}
