using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Enums;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Domain.Projections;
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
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

        if (order != null && order.OrderItems != null)
        {
            foreach (var item in order.OrderItems)
            {
                await _context.Entry(item)
                              .Reference(i => i.Product)
                              .Query()
                              .IgnoreQueryFilters()
                              .LoadAsync();
            }
        }
        return order;
    }

    public async Task<Order?> GetOrderByIdAndUserAsync(Guid orderId, Guid userId, Guid tenantId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ApplicationUserId == userId && o.TenantId == tenantId);

        if (order != null && order.OrderItems != null)
        {
            foreach (var item in order.OrderItems)
            {
                await _context.Entry(item)
                              .Reference(i => i.Product)
                              .Query()
                              .IgnoreQueryFilters()
                              .LoadAsync();
            }
        }
        return order;
    }

    public async Task<IEnumerable<Order>> GetOrdersByUserIdAndTenantAsync(Guid userId, Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.ApplicationUserId == userId && o.TenantId == tenantId)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersByTenantIdAsync(Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
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

    public async Task<IEnumerable<Order>> GetOrdersWithItemsAndProductCategoriesAsync(Guid userId, Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.ApplicationUserId == userId && o.TenantId == tenantId && o.OrderItems.Any())
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
            .AsNoTracking()
            .ToListAsync();
    }

    public IQueryable<Order> GetFilteredOrdersQuery(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate,
        string? filterDimension = null, string? filterValue = null)
    {
        var query = _context.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.OrderDate >= startDate && o.OrderDate < endDate);

        if (string.IsNullOrEmpty(filterDimension) || string.IsNullOrEmpty(filterValue))
        {
            return query;
        }

        switch (filterDimension.ToLowerInvariant())
        {
            case "category":
                if (Guid.TryParse(filterValue, out Guid categoryId))
                {
                    query = query.Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.CategoryId == categoryId));
                }
                break;
            case "product":
                if (Guid.TryParse(filterValue, out Guid productId))
                {
                    query = query.Where(o => o.OrderItems.Any(oi => oi.ProductId == productId));
                }
                break;
            case "status":
                if (Enum.TryParse<OrderStatus>(filterValue, true, out OrderStatus status))
                {
                    query = query.Where(o => o.Status == status);
                }
                break;
            case "customer":
                if (Guid.TryParse(filterValue, out Guid customerId))
                {
                    query = query.Where(o => o.ApplicationUserId == customerId);
                }
                break;
        }
        return query;
    }

    public async Task<(decimal TotalRevenue, int TotalOrders, int TotalUniqueCustomers)> GetOverallSummaryAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var summary = await preFilteredOrdersQuery
            .GroupBy(o => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(o => o.TotalAmount),
                TotalUniqueCustomers = g.Select(o => o.ApplicationUserId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        return summary != null ? (summary.TotalRevenue, summary.TotalOrders, summary.TotalUniqueCustomers) : (0, 0, 0);
    }

    public async Task<List<OrderAggregationByTime>> GetOrdersAggregatedByTimeDimensionAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, string timeDimension, IQueryable<Order> preFilteredOrdersQuery)
    {
        return timeDimension.ToLowerInvariant() switch
        {
            "day" => await preFilteredOrdersQuery.GroupBy(o => o.OrderDate.Date)
                .Select(g => new OrderAggregationByTime(g.Key.ToString("yyyy-MM-dd"), g.Sum(o => o.TotalAmount), g.Count()))
                .OrderBy(r => r.PeriodLabel).ToListAsync(),
            "month" => await preFilteredOrdersQuery.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new OrderAggregationByTime($"{g.Key.Year}-{g.Key.Month:D2}", g.Sum(o => o.TotalAmount), g.Count()))
                .OrderBy(r => r.PeriodLabel).ToListAsync(),
            "year" => await preFilteredOrdersQuery.GroupBy(o => o.OrderDate.Year)
                .Select(g => new OrderAggregationByTime(g.Key.ToString(), g.Sum(o => o.TotalAmount), g.Count()))
                .OrderBy(r => r.PeriodLabel).ToListAsync(),
            _ => new List<OrderAggregationByTime>(),
        };
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCategoryAsync(
         Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var ordersInPeriod = await preFilteredOrdersQuery.Select(o => o.Id).ToListAsync();
        if (!ordersInPeriod.Any()) return new List<OrderAggregationByEntity>();

        return await _context.OrderItems
            .AsNoTracking()
            .Where(oi => ordersInPeriod.Contains(oi.OrderId))
            .Include(oi => oi.Product).ThenInclude(p => p!.Category)
            .Where(oi => oi.Product != null && oi.Product.Category != null)
            .GroupBy(oi => new { oi.Product!.CategoryId, Name = oi.Product.Category!.Name })
            .Select(g => new OrderAggregationByEntity(
                g.Key.CategoryId,
                g.Key.Name ?? "Sin Categoría",
                g.Sum(oi => oi.Quantity * oi.UnitPrice),
                g.Select(oi => oi.OrderId).Distinct().Count()
            ))
            .OrderByDescending(r => r.TotalAmount)
            .ToListAsync();
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByProductAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var ordersInPeriod = await preFilteredOrdersQuery.Select(o => o.Id).ToListAsync();
        if (!ordersInPeriod.Any()) return new List<OrderAggregationByEntity>();

        return await _context.OrderItems
            .AsNoTracking()
            .Where(oi => ordersInPeriod.Contains(oi.OrderId))
            .Include(oi => oi.Product)
            .Where(oi => oi.Product != null)
            .GroupBy(oi => new { oi.Product!.Id, Name = oi.Product.Name })
            .Select(g => new OrderAggregationByEntity(
                g.Key.Id,
                g.Key.Name ?? "Sin Producto",
                g.Sum(oi => oi.Quantity * oi.UnitPrice),
                g.Select(oi => oi.OrderId).Distinct().Count()
            ))
            .OrderByDescending(r => r.TotalAmount)
            .ToListAsync();
    }

    public async Task<List<OrderAggregationByStatus>> GetOrdersAggregatedByStatusAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        return await preFilteredOrdersQuery
            .GroupBy(o => o.Status)
            .Select(g => new OrderAggregationByStatus(
                g.Key,
                g.Sum(o => o.TotalAmount),
                g.Count()
            ))
            .OrderBy(r => r.Status)
            .ToListAsync();
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCustomerAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        return await preFilteredOrdersQuery
            .Include(o => o.User)
            .Where(o => o.User != null && o.ApplicationUserId.HasValue)
            .GroupBy(o => new { CustomerId = o.ApplicationUserId!.Value, CustomerName = o.User!.Name })
            .Select(g => new OrderAggregationByEntity(
                g.Key.CustomerId,
                g.Key.CustomerName ?? "Cliente Desconocido",
                g.Sum(o => o.TotalAmount),
                g.Count()
            ))
            .OrderByDescending(r => r.TotalAmount)
            .ToListAsync();
    }
}
